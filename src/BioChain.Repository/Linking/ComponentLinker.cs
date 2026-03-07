using System.Text.Json;
using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Linking;

/// <summary>
/// Routes parsed DSL lines to the appropriate repository for entity creation.
/// Uses DbContext directly for simple CRUD; delegates to repositories only
/// for types that have complex query methods used elsewhere (Gate, Module, Stimuli).
/// </summary>
public class ComponentLinker(
    BioChainDbContext db,
    IGateRepository gates,
    IModuleRepository modules,
    IStimuliRepository stimuli) : IComponentLinker
{
    private static readonly Lazy<Dictionary<string, (long? MinMs, long? MaxMs)>> TauLookup = new(() =>
    {
        var paths = new[] { "Data/TauConstants.json", "../BioChain.Repository/Data/TauConstants.json" };
        foreach (var p in paths)
        {
            var full = Path.GetFullPath(p, AppContext.BaseDirectory);
            if (!File.Exists(full)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(full));
            var dict = new Dictionary<string, (long?, long?)>(StringComparer.OrdinalIgnoreCase);

            foreach (var category in doc.RootElement.EnumerateObject())
            {
                if (category.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var chemical in category.Value.EnumerateObject())
                {
                    if (chemical.Value.ValueKind != JsonValueKind.Object) continue;
                    long? minMs = chemical.Value.TryGetProperty("tau_min_ms", out var minMsProp) ? minMsProp.GetInt64() : null;
                    long? maxMs = chemical.Value.TryGetProperty("tau_max_ms", out var maxMsProp) ? maxMsProp.GetInt64() : null;
                    if (minMs is not null || maxMs is not null)
                        dict[chemical.Name] = (minMs, maxMs);
                }
            }
            return dict;
        }
        return new Dictionary<string, (long?, long?)>();
    });

    /// <summary>
    /// Additional signal code aliases that the LLM may produce but aren't in TauConstants.json.
    /// </summary>
    private static readonly HashSet<string> AdditionalSignalAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "MEL", "ATP", "CORTISOL", "EPINEPHRINE", "ADRENALINE", "NORADRENALINE",
        "INS", "ANANDAMIDE", "ANA", "ECB", "SUBP", "SUBSTANCE_P",
        "SEROTONIN", "DOPAMINE", "NOREPINEPHRINE", "GLUTAMATE",
        "OXYTOCIN", "VASOPRESSIN", "THYROID", "CRF",
        "TNF", "TNFA", "IL1", "IFN", "NFKB", "ALLO",
    };

    /// <summary>
    /// Validates that a signal code represents a known biochemical entity.
    /// Rejects behavioral abstractions (ATTENTION, ACTION, BEHAVIOR, etc.)
    /// that the LLM sometimes hallucinates as signal codes.
    /// </summary>
    private static bool IsValidSignalCode(string code)
    {
        if (TauLookup.Value.ContainsKey(code)) return true;
        if (AdditionalSignalAliases.Contains(code)) return true;

        // Check base code for region-suffixed codes (DA_VTA → DA, 5HT_DRN → 5HT)
        var idx = code.IndexOf('_');
        if (idx > 0)
        {
            var baseCode = code[..idx];
            return TauLookup.Value.ContainsKey(baseCode) || AdditionalSignalAliases.Contains(baseCode);
        }

        return false;
    }

    public async Task LinkAsync(AnalysisEntity analysis, BioChainParser.ParsedLine line,
        Guid subjectId, CancellationToken ct = default)
    {
        switch (line.Tag)
        {
            case "SIGNAL":
            case "STATE":
            {
                var sig = BioChainParser.ExtractSignal(line.Formula);
                if (sig is null) break;

                // Reject behavioral abstractions (ATTENTION, ACTION, COGNITION, etc.)
                if (!IsValidSignalCode(sig.Value.Code)) break;

                int? regionId = null;
                if (sig.Value.Region is not null)
                {
                    var region = await GetOrCreateRegionAsync(sig.Value.Region, subjectId, ct);
                    regionId = region.Id;
                }

                var tau = TauLookup.Value.GetValueOrDefault(sig.Value.Code);
                db.Signals.Add(new SignalEntity
                {
                    SubjectId = subjectId,
                    Type = sig.Value.Type,
                    Code = sig.Value.Code,
                    State = sig.Value.State ?? "\u2248", // ≈
                    RegionId = regionId,
                    TauMinMs = tau.MinMs,
                    TauMaxMs = tau.MaxMs,
                    AnalysisId = analysis.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "RECEPTOR":
            {
                var rec = BioChainParser.ExtractReceptor(line.Formula);
                if (rec is null) break;

                // Store signal code directly — no parent lookup needed
                db.Receptors.Add(new ReceptorEntity
                {
                    SubjectId = subjectId,
                    SignalCode = rec.Value.SignalCode,
                    SignalType = BioChainParser.InferSignalType(rec.Value.SignalCode),
                    Code = rec.Value.Code,
                    State = rec.Value.State ?? "active",
                    Subtype = rec.Value.Subtype,
                    AnalysisId = analysis.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "GATE":
            {
                var gate = BioChainParser.ExtractGate(line.Formula);
                if (gate is null) break;
                await gates.CreateAsync(new GateEntity
                {
                    SubjectId = subjectId,
                    Code = gate.Value.Expression,
                    Type = gate.Value.Type,
                    AnalysisId = analysis.Id,
                }, ct);
                break;
            }

            case "LLM_GATE":
            {
                var gateEntity = new GateEntity
                {
                    SubjectId = subjectId,
                    Type = "llm",
                    AnalysisId = analysis.Id,
                };

                if (line.Formula.Contains("PROMPT:") || line.Formula.Contains("MODEL:") ||
                    line.Formula.Contains("FALLBACK:"))
                {
                    var blockLines = line.Formula.Split('\n');
                    var name = blockLines[0].Replace("{", "").Trim();
                    gateEntity.Code = name.Length > 0 ? name : "llm_gate";

                    foreach (var bl in blockLines)
                    {
                        var trimmed = bl.Trim();
                        if (trimmed.StartsWith("PROMPT:"))
                            gateEntity.Prompt = trimmed["PROMPT:".Length..].Trim().Trim('"');
                        else if (trimmed.StartsWith("MODEL:"))
                            gateEntity.Model = trimmed["MODEL:".Length..].Trim();
                        else if (trimmed.StartsWith("FALLBACK:"))
                            gateEntity.FallbackExpr = trimmed["FALLBACK:".Length..].Trim();
                        else if (trimmed.StartsWith("TIMEOUT:"))
                        {
                            if (int.TryParse(trimmed["TIMEOUT:".Length..].Trim(), out var timeout))
                                gateEntity.TimeoutMs = timeout;
                        }
                        else if (trimmed.StartsWith("CACHE:"))
                        {
                            if (int.TryParse(trimmed["CACHE:".Length..].Trim(), out var cache))
                                gateEntity.CacheMs = cache;
                        }
                    }
                }
                else
                {
                    var gate = BioChainParser.ExtractGate(line.Formula);
                    var gateCode = gate?.Expression ?? line.Formula;
                    gateEntity.Code = gateCode.Length > 100 ? gateCode[..100] : gateCode;
                }

                await gates.CreateAsync(gateEntity, ct);
                break;
            }

            case "LIMITER":
            {
                var lim = BioChainParser.ExtractLimiter(line.Formula);
                if (lim is null) break;

                var limiterEntity = new LimiterEntity
                {
                    SubjectId = subjectId,
                    Code = lim.Value.Code,
                    Activity = lim.Value.Activity ?? "\u2248",
                    RateLimiting = lim.Value.RateLimiting,
                    Reaction = lim.Value.Reaction,
                    AnalysisId = analysis.Id,
                };

                // Resolve reaction target to a signal FK
                if (!string.IsNullOrEmpty(lim.Value.Reaction))
                {
                    var reactionRef = BioChainParser.ExtractReactionSignalRef(lim.Value.Reaction);
                    if (reactionRef is not null)
                    {
                        int? regionId = null;
                        if (reactionRef.Value.Region is not null)
                            regionId = (await GetOrCreateRegionAsync(reactionRef.Value.Region, subjectId, ct)).Id;

                        var target = await GetOrCreateSignalAsync(
                            subjectId, analysis.Id, reactionRef.Value.Code, regionId, ct);
                        if (target is not null)
                            limiterEntity.TargetId = target.Id;
                    }
                }

                db.Limiters.Add(limiterEntity);
                await db.SaveChangesAsync(ct);
                break;
            }

            case "TRANSPORT":
            {
                var tr = BioChainParser.ExtractTransporter(line.Formula);
                if (tr is null) break;

                // Infer signal code from transporter naming convention (DAT→DA, SERT→5HT, etc.)
                var signalCode = BioChainParser.InferTransporterSignalCode(tr.Value.Code);
                var signalType = signalCode is not null ? BioChainParser.InferSignalType(signalCode) : null;

                db.Transporters.Add(new TransporterEntity
                {
                    SubjectId = subjectId,
                    SignalCode = signalCode,
                    SignalType = signalType,
                    Code = tr.Value.Code,
                    State = tr.Value.State ?? "active",
                    Clearance = tr.Value.Clearance ?? "\u2248",
                    AnalysisId = analysis.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "INTERFACE":
            {
                var iface = BioChainParser.ExtractInterface(line.Formula);
                if (iface is null) break;
                var srcRegion = await GetOrCreateRegionAsync(iface.Value.Source, subjectId, ct);
                var tgtRegion = await GetOrCreateRegionAsync(iface.Value.Target, subjectId, ct);
                db.Interfaces.Add(new InterfaceEntity
                {
                    SubjectId = subjectId,
                    Code = $"{iface.Value.Source}\u2192{iface.Value.Target}",
                    SourceRegionId = srcRegion.Id,
                    TargetRegionId = tgtRegion.Id,
                    Pathway = iface.Value.Pathway,
                    AnalysisId = analysis.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "CONSTRAINT":
            case "EQUILIBRIUM":
            case "BOUNDARY":
            case "CONSERVE":
            {
                db.Constraints.Add(new ConstraintDefEntity
                {
                    SubjectId = subjectId,
                    Type = line.Tag.ToLowerInvariant(),
                    Expression = line.Formula,
                    AnalysisId = analysis.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "TOOL":
            {
                var toolEntity = new ToolEntity
                {
                    SubjectId = subjectId,
                    AnalysisId = analysis.Id,
                };

                if (line.Formula.Contains("INVOKE:") || line.Formula.Contains("INPUT:") ||
                    line.Formula.Contains("OUTPUT:"))
                {
                    var blockLines = line.Formula.Split('\n');
                    var name = blockLines[0].Replace("{", "").Trim();
                    var toolCode = name.Length > 0 ? name : "tool";
                    toolEntity.Code = toolCode.Length > 50 ? toolCode[..50] : toolCode;

                    foreach (var bl in blockLines)
                    {
                        var trimmed = bl.Trim();
                        if (trimmed.StartsWith("INVOKE:"))
                            toolEntity.Invoke = trimmed["INVOKE:".Length..].Trim().Trim('"');
                        else if (trimmed.StartsWith("INPUT:"))
                            toolEntity.InputRefs = trimmed["INPUT:".Length..].Trim()
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        else if (trimmed.StartsWith("OUTPUT:"))
                            toolEntity.OutputRefs = trimmed["OUTPUT:".Length..].Trim()
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        else if (trimmed.StartsWith("GATE:"))
                            toolEntity.GateExpr = trimmed["GATE:".Length..].Trim();
                        else if (trimmed.StartsWith("TIMEOUT:"))
                        {
                            if (int.TryParse(trimmed["TIMEOUT:".Length..].Trim(), out var timeout))
                                toolEntity.TimeoutMs = timeout;
                        }
                        else if (trimmed.StartsWith("RETRY:"))
                        {
                            if (int.TryParse(trimmed["RETRY:".Length..].Trim(), out var retry))
                                toolEntity.RetryCount = retry;
                        }
                        else if (trimmed.StartsWith("FALLBACK:"))
                            toolEntity.Fallback = trimmed["FALLBACK:".Length..].Trim();
                    }

                    if (string.IsNullOrEmpty(toolEntity.Invoke))
                        toolEntity.Invoke = toolEntity.Code;
                }
                else
                {
                    var code = line.Formula.Trim();
                    toolEntity.Code = code.Length > 50 ? code[..50] : code;
                    toolEntity.Invoke = line.Formula;
                }

                db.Tools.Add(toolEntity);
                await db.SaveChangesAsync(ct);
                break;
            }

            case "MODULE":
            {
                var moduleEntity = new ModuleEntity { SubjectId = subjectId };
                if (line.Formula.Contains('{'))
                {
                    var blockLines = line.Formula.Split('\n');
                    var name = blockLines[0].Replace("{", "").Trim();
                    moduleEntity.Code = (name.Length > 50 ? name[..50] : name).Length > 0
                        ? (name.Length > 50 ? name[..50] : name) : "module";
                    var bodyParts = new Dictionary<string, string>();
                    foreach (var bl in blockLines)
                    {
                        var trimmed = bl.Trim();
                        if (trimmed.StartsWith("DEF:"))
                            bodyParts["def"] = trimmed["DEF:".Length..].Trim();
                        else if (trimmed.StartsWith("IMPORT:"))
                            bodyParts["import"] = trimmed["IMPORT:".Length..].Trim();
                        else if (trimmed.StartsWith("AGENT:"))
                            moduleEntity.AgentType = trimmed["AGENT:".Length..].Trim();
                    }
                    if (bodyParts.Count > 0)
                        moduleEntity.Properties = JsonSerializer.Serialize(bodyParts);
                }
                else
                {
                    var code = line.Formula.Trim();
                    moduleEntity.Code = code.Length > 50 ? code[..50] : code;
                }
                await modules.CreateAsync(moduleEntity, ct);
                break;
            }

            case "BIND":
            case "FAIL":
                break;

            case "FORMULA":
            case "FEEDBACK":
            case "DEF":
            {
                var (gateInfo, cleanFormula) = BioChainParser.ExtractFormulaGateCondition(line.Formula);
                var (src, tgt) = BioChainParser.ExtractFormulaSignalRefs(cleanFormula);
                SignalEntity? srcSignal = null, tgtSignal = null;

                if (src is not null)
                {
                    int? srcRegionId = null;
                    if (src.Value.Region is not null)
                    {
                        var r = await GetOrCreateRegionAsync(src.Value.Region, subjectId, ct);
                        srcRegionId = r.Id;
                    }
                    srcSignal = await GetOrCreateSignalAsync(subjectId, analysis.Id, src.Value.Code, srcRegionId, ct);
                }
                if (tgt is not null && tgt != src)
                {
                    int? tgtRegionId = null;
                    if (tgt.Value.Region is not null)
                    {
                        var r = await GetOrCreateRegionAsync(tgt.Value.Region, subjectId, ct);
                        tgtRegionId = r.Id;
                    }
                    tgtSignal = await GetOrCreateSignalAsync(subjectId, analysis.Id, tgt.Value.Code, tgtRegionId, ct);
                }

                if (srcSignal is not null && tgtSignal is not null)
                {
                    var opClass = line.Tag == "FEEDBACK" ? "feedback" : "causal";
                    var op = line.Tag == "FEEDBACK" ? "\u27f3\u207b" : "\u2192";
                    var kind = line.Tag == "FEEDBACK" ? "negative_feedback" : "causal";

                    int? gateId = null;
                    string? gateCode = null, gateType = null, gateCond = null;
                    if (gateInfo is not null)
                    {
                        var structuredExpr = BioChainParser.ParseGateExpression(gateInfo.Value.Expression);
                        var gateEntity = await gates.CreateAsync(new GateEntity
                        {
                            SubjectId = subjectId,
                            Code = gateInfo.Value.Expression,
                            Type = gateInfo.Value.Type,
                            Expression = structuredExpr,
                            AnalysisId = analysis.Id,
                        }, ct);
                        gateId = gateEntity.Id;
                        gateCode = gateInfo.Value.Expression;
                        gateType = gateInfo.Value.Type;
                        gateCond = structuredExpr;
                    }

                    db.Edges.Add(new EdgeEntity
                    {
                        SubjectId = subjectId,
                        // Legacy ID-based
                        SourceType = "signal",
                        SourceId = srcSignal.Id,
                        TargetType = "signal",
                        TargetId = tgtSignal.Id,
                        // Code-based
                        SourceCode = src!.Value.Code,
                        SourceSignalType = BioChainParser.InferSignalType(src.Value.Code),
                        SourceRegion = src.Value.Region,
                        TargetCode = tgt!.Value.Code,
                        TargetSignalType = BioChainParser.InferSignalType(tgt.Value.Code),
                        TargetRegion = tgt.Value.Region,
                        RelationshipKind = kind,
                        // Gate
                        Operator = op,
                        OperatorClass = opClass,
                        GateId = gateId,
                        GateCode = gateCode,
                        GateType = gateType,
                        GateCondition = gateCond,
                        AnalysisId = analysis.Id,
                    });
                    await db.SaveChangesAsync(ct);
                }
                break;
            }

            case "PREDICTION":
            case "HYPOTHESIS":
            case "INTERVENTION":
                break;

            case "DYSREG":
            {
                await stimuli.CreateAsync(new StimuliEntity
                    { SubjectId = subjectId, Kind = "inferred", SourceText = line.Formula, Analyzed = true }, ct);

                var (dysregGateInfo, dysregCleanFormula) = BioChainParser.ExtractFormulaGateCondition(line.Formula);
                var (dSrc, dTgt) = BioChainParser.ExtractFormulaSignalRefs(dysregCleanFormula);
                if (dSrc is not null && dTgt is not null)
                {
                    int? dSrcRegionId = null;
                    if (dSrc.Value.Region is not null)
                    {
                        var r = await GetOrCreateRegionAsync(dSrc.Value.Region, subjectId, ct);
                        dSrcRegionId = r.Id;
                    }
                    int? dTgtRegionId = null;
                    if (dTgt.Value.Region is not null)
                    {
                        var r = await GetOrCreateRegionAsync(dTgt.Value.Region, subjectId, ct);
                        dTgtRegionId = r.Id;
                    }
                    var s = await GetOrCreateSignalAsync(subjectId, analysis.Id, dSrc.Value.Code, dSrcRegionId, ct);
                    var t = await GetOrCreateSignalAsync(subjectId, analysis.Id, dTgt.Value.Code, dTgtRegionId, ct);
                    if (s is null || t is null) break; // unknown signal code — skip edge

                    int? dysregGateId = null;
                    string? dysregGateCode = null, dysregGateType = null, dysregGateCond = null;
                    if (dysregGateInfo is not null)
                    {
                        var structuredExpr = BioChainParser.ParseGateExpression(dysregGateInfo.Value.Expression);
                        var gateEntity = await gates.CreateAsync(new GateEntity
                        {
                            SubjectId = subjectId,
                            Code = dysregGateInfo.Value.Expression,
                            Type = dysregGateInfo.Value.Type,
                            Expression = structuredExpr,
                            AnalysisId = analysis.Id,
                        }, ct);
                        dysregGateId = gateEntity.Id;
                        dysregGateCode = dysregGateInfo.Value.Expression;
                        dysregGateType = dysregGateInfo.Value.Type;
                        dysregGateCond = structuredExpr;
                    }

                    db.Edges.Add(new EdgeEntity
                    {
                        SubjectId = subjectId,
                        // Legacy ID-based
                        SourceType = "signal",
                        SourceId = s.Id,
                        TargetType = "signal",
                        TargetId = t.Id,
                        // Code-based
                        SourceCode = dSrc.Value.Code,
                        SourceSignalType = BioChainParser.InferSignalType(dSrc.Value.Code),
                        SourceRegion = dSrc.Value.Region,
                        TargetCode = dTgt.Value.Code,
                        TargetSignalType = BioChainParser.InferSignalType(dTgt.Value.Code),
                        TargetRegion = dTgt.Value.Region,
                        RelationshipKind = "dysregulation",
                        // Gate
                        Operator = "\u26a1",
                        OperatorClass = "dysreg",
                        GateId = dysregGateId,
                        GateCode = dysregGateCode,
                        GateType = dysregGateType,
                        GateCondition = dysregGateCond,
                        AnalysisId = analysis.Id,
                    });
                    await db.SaveChangesAsync(ct);
                }
                break;
            }
        }
    }

    // -- Inlined from deleted repositories --

    private async Task<SignalEntity?> GetCurrentSignalByCodeAsync(
        Guid subjectId, string code, int? regionId = null, CancellationToken ct = default)
    {
        var query = db.Signals
            .Where(s => s.SubjectId == subjectId && s.Code == code);

        if (regionId is not null)
            query = query.Where(s => s.RegionId == regionId);

        return await query
            .OrderByDescending(s => s.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns the most recent signal with the given code, or auto-creates a placeholder
    /// signal if none exists. Used by FORMULA/FEEDBACK/DYSREG handlers so edges are never
    /// silently dropped when the LLM references signal codes without a preceding SIGNAL: line.
    /// Returns null for codes that fail biochemical vocabulary validation.
    /// </summary>
    private async Task<SignalEntity?> GetOrCreateSignalAsync(
        Guid subjectId, int analysisId, string code,
        int? regionId = null, CancellationToken ct = default)
    {
        // Always check for an existing signal first (may have been created before validation was added)
        var existing = await GetCurrentSignalByCodeAsync(subjectId, code, regionId, ct);
        if (existing is not null) return existing;

        // Don't auto-create signals for unknown/behavioral codes (ATTENTION, ACTION, etc.)
        if (!IsValidSignalCode(code)) return null;

        var tau = TauLookup.Value.GetValueOrDefault(code);
        var signal = new SignalEntity
        {
            SubjectId = subjectId,
            Code = code,
            Type = BioChainParser.InferSignalType(code),
            State = "\u2248", // ≈ neutral baseline
            RegionId = regionId,
            TauMinMs = tau.MinMs,
            TauMaxMs = tau.MaxMs,
            AnalysisId = analysisId,
        };
        db.Signals.Add(signal);
        await db.SaveChangesAsync(ct);
        return signal;
    }

    private async Task<RegionEntity> GetOrCreateRegionAsync(
        string code, Guid subjectId, CancellationToken ct)
    {
        var existing = await db.Regions
            .Where(r => r.SubjectId == subjectId && r.Code == code)
            .OrderByDescending(r => r.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null) return existing;

        var region = new RegionEntity { SubjectId = subjectId, Code = code };
        db.Regions.Add(region);
        await db.SaveChangesAsync(ct);
        return region;
    }

    /// <inheritdoc />
    public async Task ConnectOrphanedSignalsAsync(Guid subjectId, CancellationToken ct = default)
    {
        // Collect signal codes/IDs that participate in any relationship
        // Check both ID-based and code-based connections
        var edgeSourceIds = await db.Edges
            .Where(e => e.SubjectId == subjectId && e.SourceId != null)
            .Select(e => e.SourceId!.Value)
            .ToListAsync(ct);
        var edgeTargetIds = await db.Edges
            .Where(e => e.SubjectId == subjectId && e.TargetId != null)
            .Select(e => e.TargetId!.Value)
            .ToListAsync(ct);
        var edgeSourceCodes = await db.Edges
            .Where(e => e.SubjectId == subjectId && e.SourceCode != null)
            .Select(e => e.SourceCode!)
            .Distinct()
            .ToListAsync(ct);
        var edgeTargetCodes = await db.Edges
            .Where(e => e.SubjectId == subjectId && e.TargetCode != null)
            .Select(e => e.TargetCode!)
            .Distinct()
            .ToListAsync(ct);
        var edgeConnectedCodes = edgeSourceCodes.Concat(edgeTargetCodes).Distinct().ToList();

        var limiterTargetIds = await db.Limiters
            .Where(l => l.SubjectId == subjectId && l.TargetId != null)
            .Select(l => l.TargetId!.Value)
            .ToListAsync(ct);

        // Code-based: receptors and transporters now reference signals by code
        var receptorSignalCodes = await db.Receptors
            .Where(r => r.SubjectId == subjectId && r.SignalCode != null)
            .Select(r => r.SignalCode!)
            .Distinct()
            .ToListAsync(ct);
        var receptorSignalIds = await db.Receptors
            .Where(r => r.SubjectId == subjectId && r.SignalId != null)
            .Select(r => r.SignalId!.Value)
            .ToListAsync(ct);

        var transporterSignalCodes = await db.Transporters
            .Where(t => t.SubjectId == subjectId && t.SignalCode != null)
            .Select(t => t.SignalCode!)
            .Distinct()
            .ToListAsync(ct);
        var transporterSignalIds = await db.Transporters
            .Where(t => t.SubjectId == subjectId && t.SignalId != null)
            .Select(t => t.SignalId!.Value)
            .ToListAsync(ct);

        var connectedIds = new HashSet<int>(edgeSourceIds
            .Concat(edgeTargetIds)
            .Concat(limiterTargetIds)
            .Concat(receptorSignalIds)
            .Concat(transporterSignalIds));

        var connectedCodes = new HashSet<string>(
            edgeConnectedCodes.Concat(receptorSignalCodes).Concat(transporterSignalCodes)
                .Where(c => c is not null)!,
            StringComparer.OrdinalIgnoreCase);

        var allSignals = await db.Signals
            .Where(s => s.SubjectId == subjectId)
            .ToListAsync(ct);

        // A signal is connected if referenced by ID or by code
        var orphans = allSignals
            .Where(s => !connectedIds.Contains(s.Id) && !connectedCodes.Contains(s.Code))
            .ToList();
        if (orphans.Count == 0) return;

        // Build region → best connected signal lookup
        bool IsConnected(SignalEntity s) =>
            connectedIds.Contains(s.Id) || connectedCodes.Contains(s.Code);

        var connectedByRegion = allSignals
            .Where(s => IsConnected(s) && s.RegionId is not null)
            .GroupBy(s => s.RegionId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var anyConnected = allSignals.FirstOrDefault(IsConnected);

        foreach (var orphan in orphans)
        {
            SignalEntity? target = null;

            // Prefer same-region connected signal
            if (orphan.RegionId is not null)
                connectedByRegion.TryGetValue(orphan.RegionId.Value, out target);

            // Fallback: any connected signal
            target ??= anyConnected;
            if (target is null) continue;

            db.Edges.Add(new EdgeEntity
            {
                SubjectId = subjectId,
                // Legacy ID-based
                SourceType = "signal",
                SourceId = orphan.Id,
                TargetType = "signal",
                TargetId = target.Id,
                // Code-based
                SourceCode = orphan.Code,
                SourceSignalType = orphan.Type,
                TargetCode = target.Code,
                TargetSignalType = target.Type,
                RelationshipKind = "modulation",
                Operator = "\u22a9", // ⊩ modulates
                OperatorClass = "causal",
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
