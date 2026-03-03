using System.Text.Json;
using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;

namespace BioChain.Repository.Linking;

/// <summary>
/// Routes parsed DSL lines to the appropriate repository for entity creation.
/// Extracted verbatim from AnalyzeService.LinkComponentAsync.
/// </summary>
public class ComponentLinker(
    ISignalRepository signals,
    IReceptorRepository receptors,
    IGateRepository gates,
    ILimiterRepository limiters,
    ITransporterRepository transporters,
    IInterfaceRepository interfaces,
    IEdgeRepository edges,
    IModuleRepository modules,
    IConstraintDefRepository constraints,
    IToolRepository tools,
    IRegionRepository regions,
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
                    var region = await regions.GetOrCreateAsync(sig.Value.Region, subjectId, ct);
                    regionId = region.Id;
                }

                var tau = TauLookup.Value.GetValueOrDefault(sig.Value.Code);
                await signals.CreateAsync(new SignalEntity
                {
                    SubjectId = subjectId,
                    Type = sig.Value.Type,
                    Code = sig.Value.Code,
                    State = sig.Value.State ?? "\u2248", // ≈
                    RegionId = regionId,
                    TauMinMs = tau.MinMs,
                    TauMaxMs = tau.MaxMs,
                    ProtocolId = protocol.Id,
                }, ct);
                break;
            }

            case "RECEPTOR":
            {
                var rec = BioChainParser.ExtractReceptor(line.Formula);
                if (rec is null) break;
                var parent = await signals.GetCurrentByCodeAsync(subjectId, rec.Value.SignalCode, ct: ct);
                if (parent is null) break;
                await receptors.CreateAsync(new ReceptorEntity
                {
                    SubjectId = subjectId,
                    SignalId = parent.Id,
                    Code = rec.Value.Code,
                    State = rec.Value.State ?? "active",
                    Subtype = rec.Value.Subtype,
                    ProtocolId = protocol.Id,
                }, ct);
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
                await limiters.CreateAsync(new LimiterEntity
                {
                    SubjectId = subjectId,
                    Code = lim.Value.Code,
                    Activity = lim.Value.Activity ?? "\u2248",
                    RateLimiting = lim.Value.RateLimiting,
                    Reaction = lim.Value.Reaction,
                    ProtocolId = protocol.Id,
                }, ct);
                break;
            }

            case "TRANSPORT":
            {
                var tr = BioChainParser.ExtractTransporter(line.Formula);
                if (tr is null) break;
                var signalCode = BioChainParser.MapTransporterToSignal(tr.Value.Code);
                var parent = signalCode is not null
                    ? await signals.GetCurrentByCodeAsync(subjectId, signalCode, ct: ct)
                    : null;
                if (parent is null) break;
                await transporters.CreateAsync(new TransporterEntity
                {
                    SubjectId = subjectId,
                    SignalId = parent.Id,
                    Code = tr.Value.Code,
                    State = tr.Value.State ?? "active",
                    Clearance = tr.Value.Clearance ?? "\u2248",
                    ProtocolId = protocol.Id,
                }, ct);
                break;
            }

            case "INTERFACE":
            {
                var iface = BioChainParser.ExtractInterface(line.Formula);
                if (iface is null) break;
                var srcRegion = await regions.GetOrCreateAsync(iface.Value.Source, subjectId, ct);
                var tgtRegion = await regions.GetOrCreateAsync(iface.Value.Target, subjectId, ct);
                await interfaces.CreateAsync(new InterfaceEntity
                {
                    SubjectId = subjectId,
                    Code = $"{iface.Value.Source}\u2192{iface.Value.Target}",
                    SourceRegionId = srcRegion.Id,
                    TargetRegionId = tgtRegion.Id,
                    Pathway = iface.Value.Pathway,
                    ProtocolId = protocol.Id,
                }, ct);
                break;
            }

            case "CONSTRAINT":
            case "EQUILIBRIUM":
            case "BOUNDARY":
            case "CONSERVE":
            {
                await constraints.CreateAsync(new ConstraintDefEntity
                {
                    SubjectId = subjectId,
                    Type = line.Tag.ToLowerInvariant(),
                    Expression = line.Formula,
                    ProtocolId = protocol.Id,
                }, ct);
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

                await tools.CreateAsync(toolEntity, ct);
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
                        var r = await regions.GetOrCreateAsync(src.Value.Region, subjectId, ct);
                        srcRegionId = r.Id;
                    }
                    srcSignal = await signals.GetCurrentByCodeAsync(subjectId, src.Value.Code, srcRegionId, ct);
                }
                if (tgt is not null && tgt != src)
                {
                    int? tgtRegionId = null;
                    if (tgt.Value.Region is not null)
                    {
                        var r = await regions.GetOrCreateAsync(tgt.Value.Region, subjectId, ct);
                        tgtRegionId = r.Id;
                    }
                    tgtSignal = await signals.GetCurrentByCodeAsync(subjectId, tgt.Value.Code, tgtRegionId, ct);
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

                    await edges.CreateAsync(new EdgeEntity
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
                    }, ct);
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
                    var s = await signals.GetCurrentByCodeAsync(subjectId, dSrc.Value.Code, ct: ct);
                    var t = await signals.GetCurrentByCodeAsync(subjectId, dTgt.Value.Code, ct: ct);
                    if (s is not null && t is not null)
                    {
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

                        await edges.CreateAsync(new EdgeEntity
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
                        }, ct);
                    }
                }
                break;
            }
        }
    }
}
