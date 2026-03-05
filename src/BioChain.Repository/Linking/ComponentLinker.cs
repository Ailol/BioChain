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

    public async Task LinkAsync(ProtocolEntity protocol, BioChainParser.ParsedLine line,
        Guid subjectId, CancellationToken ct = default)
    {
        switch (line.Tag)
        {
            case "SIGNAL":
            case "STATE":
            {
                var sig = BioChainParser.ExtractSignal(line.Formula);
                if (sig is null) break;

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
                    ProtocolId = protocol.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "RECEPTOR":
            {
                var rec = BioChainParser.ExtractReceptor(line.Formula);
                if (rec is null) break;
                var parent = await GetCurrentSignalByCodeAsync(subjectId, rec.Value.SignalCode, ct: ct);
                if (parent is null) break;
                db.Receptors.Add(new ReceptorEntity
                {
                    SubjectId = subjectId,
                    SignalId = parent.Id,
                    Code = rec.Value.Code,
                    State = rec.Value.State ?? "active",
                    Subtype = rec.Value.Subtype,
                    ProtocolId = protocol.Id,
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
                    ProtocolId = protocol.Id,
                }, ct);
                break;
            }

            case "LLM_GATE":
            {
                var gateEntity = new GateEntity
                {
                    SubjectId = subjectId,
                    Type = "llm",
                    ProtocolId = protocol.Id,
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
                    ProtocolId = protocol.Id,
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
                            subjectId, protocol.Id, reactionRef.Value.Code, regionId, ct);
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
                var signalCode = BioChainParser.MapTransporterToSignal(tr.Value.Code);
                var parent = signalCode is not null
                    ? await GetCurrentSignalByCodeAsync(subjectId, signalCode, ct: ct)
                    : null;
                if (parent is null) break;
                db.Transporters.Add(new TransporterEntity
                {
                    SubjectId = subjectId,
                    SignalId = parent.Id,
                    Code = tr.Value.Code,
                    State = tr.Value.State ?? "active",
                    Clearance = tr.Value.Clearance ?? "\u2248",
                    ProtocolId = protocol.Id,
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
                    ProtocolId = protocol.Id,
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
                    ProtocolId = protocol.Id,
                });
                await db.SaveChangesAsync(ct);
                break;
            }

            case "TOOL":
            {
                var toolEntity = new ToolEntity
                {
                    SubjectId = subjectId,
                    ProtocolId = protocol.Id,
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
                    srcSignal = await GetOrCreateSignalAsync(subjectId, protocol.Id, src.Value.Code, srcRegionId, ct);
                }
                if (tgt is not null && tgt != src)
                {
                    int? tgtRegionId = null;
                    if (tgt.Value.Region is not null)
                    {
                        var r = await GetOrCreateRegionAsync(tgt.Value.Region, subjectId, ct);
                        tgtRegionId = r.Id;
                    }
                    tgtSignal = await GetOrCreateSignalAsync(subjectId, protocol.Id, tgt.Value.Code, tgtRegionId, ct);
                }

                if (srcSignal is not null && tgtSignal is not null)
                {
                    var opClass = line.Tag == "FEEDBACK" ? "feedback" : "causal";
                    var op = line.Tag == "FEEDBACK" ? "\u27f3\u207b" : "\u2192";

                    int? gateId = null;
                    if (gateInfo is not null)
                    {
                        var structuredExpr = BioChainParser.ParseGateExpression(gateInfo.Value.Expression);
                        var gateEntity = await gates.CreateAsync(new GateEntity
                        {
                            SubjectId = subjectId,
                            Code = gateInfo.Value.Expression,
                            Type = gateInfo.Value.Type,
                            Expression = structuredExpr,
                            ProtocolId = protocol.Id,
                        }, ct);
                        gateId = gateEntity.Id;
                    }

                    db.Edges.Add(new EdgeEntity
                    {
                        SubjectId = subjectId,
                        SourceType = "signal",
                        SourceId = srcSignal.Id,
                        TargetType = "signal",
                        TargetId = tgtSignal.Id,
                        Operator = op,
                        OperatorClass = opClass,
                        GateId = gateId,
                        ProtocolId = protocol.Id,
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
                    var s = await GetOrCreateSignalAsync(subjectId, protocol.Id, dSrc.Value.Code, dSrcRegionId, ct);
                    var t = await GetOrCreateSignalAsync(subjectId, protocol.Id, dTgt.Value.Code, dTgtRegionId, ct);

                    int? dysregGateId = null;
                    if (dysregGateInfo is not null)
                    {
                        var structuredExpr = BioChainParser.ParseGateExpression(dysregGateInfo.Value.Expression);
                        var gateEntity = await gates.CreateAsync(new GateEntity
                        {
                            SubjectId = subjectId,
                            Code = dysregGateInfo.Value.Expression,
                            Type = dysregGateInfo.Value.Type,
                            Expression = structuredExpr,
                            ProtocolId = protocol.Id,
                        }, ct);
                        dysregGateId = gateEntity.Id;
                    }

                    db.Edges.Add(new EdgeEntity
                    {
                        SubjectId = subjectId,
                        SourceType = "signal",
                        SourceId = s.Id,
                        TargetType = "signal",
                        TargetId = t.Id,
                        Operator = "\u26a1",
                        OperatorClass = "dysreg",
                        GateId = dysregGateId,
                        ProtocolId = protocol.Id,
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
    /// </summary>
    private async Task<SignalEntity> GetOrCreateSignalAsync(
        Guid subjectId, int protocolId, string code,
        int? regionId = null, CancellationToken ct = default)
    {
        var existing = await GetCurrentSignalByCodeAsync(subjectId, code, regionId, ct);
        if (existing is not null) return existing;

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
            ProtocolId = protocolId,
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
        // Collect all signal IDs that participate in any relationship
        var edgeSourceIds = await db.Edges
            .Where(e => e.SubjectId == subjectId)
            .Select(e => e.SourceId)
            .ToListAsync(ct);
        var edgeTargetIds = await db.Edges
            .Where(e => e.SubjectId == subjectId)
            .Select(e => e.TargetId)
            .ToListAsync(ct);
        var edgeIds = edgeSourceIds.Concat(edgeTargetIds).Distinct().ToList();

        var limiterTargetIds = await db.Limiters
            .Where(l => l.SubjectId == subjectId && l.TargetId != null)
            .Select(l => l.TargetId!.Value)
            .ToListAsync(ct);

        var receptorSignalIds = await db.Receptors
            .Where(r => r.SubjectId == subjectId)
            .Select(r => r.SignalId)
            .ToListAsync(ct);

        var transporterSignalIds = await db.Transporters
            .Where(t => t.SubjectId == subjectId)
            .Select(t => t.SignalId)
            .ToListAsync(ct);

        var connected = new HashSet<int>(edgeIds
            .Concat(limiterTargetIds)
            .Concat(receptorSignalIds)
            .Concat(transporterSignalIds));

        var allSignals = await db.Signals
            .Where(s => s.SubjectId == subjectId)
            .ToListAsync(ct);

        var orphans = allSignals.Where(s => !connected.Contains(s.Id)).ToList();
        if (orphans.Count == 0) return;

        // Build region → best connected signal lookup (prefer most-connected)
        var connectedByRegion = allSignals
            .Where(s => connected.Contains(s.Id) && s.RegionId is not null)
            .GroupBy(s => s.RegionId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var anyConnected = allSignals.FirstOrDefault(s => connected.Contains(s.Id));

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
                SourceType = "signal",
                SourceId = orphan.Id,
                TargetType = "signal",
                TargetId = target.Id,
                Operator = "\u22a9", // ⊩ modulates
                OperatorClass = "causal",
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
