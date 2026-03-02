using System.Text.Json;
using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.AI;

namespace BioChain.Service;

public class AnalyzeService(
    IChatClient engine,
    IStimuliRepository stimuli,
    ISignalRepository signals,
    IReceptorRepository receptors,
    IGateRepository gates,
    ILimiterRepository limiters,
    ITransporterRepository transporters,
    IInterfaceRepository interfaces,
    IProtocolRepository protocols,
    IRegionRepository regions,
    IEdgeRepository edges,
    IModuleRepository modules,
    IConstraintDefRepository constraints,
    IToolRepository tools)
{
    private static readonly string? SystemPrompt = LoadSystemPrompt();

    private static readonly Dictionary<string, string> Preambles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chat"] = "Analyze this conversation for neurochemical cascades:",
        ["cv"] = "Analyze this CV/resume for neurochemical personality patterns:",
        ["notes"] = "Analyze these clinical notes:",
        ["journal"] = "Analyze this journal entry for neurochemical patterns:",
        ["psych"] = "Analyze this psychological assessment:",
        ["document_by"] = "Analyze this document written by the subject:",
        ["document_about"] = "Analyze this document about the subject:",
        ["observation"] = "Analyze this observation:",
    };

    private static readonly Lazy<Dictionary<string, (long? MinMs, long? MaxMs)>> _tauLookup = new(() =>
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

    public async Task<AnalyzeResult> AnalyzeAsync(Guid subjectId, string text, string kind,
        CancellationToken ct = default)
    {
        // 1. Log raw input
        var entry = await stimuli.CreateAsync(new StimuliEntity
        {
            SubjectId = subjectId,
            Kind = kind,
            SourceText = text,
        }, ct);

        // 2. Call biochain-engine (with system prompt if available, for non-fine-tuned models)
        var userMessage = BuildPrompt(text, kind);
        var options = new ChatOptions
        {
            MaxOutputTokens = 4096,
            // Qwen3.5 instruct (non-thinking) mode — general/reasoning params
            Temperature = 0.7f,
            TopP = 0.8f,
            TopK = 20,
            PresencePenalty = 1.5f,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["chat_template_kwargs"] = new Dictionary<string, object> { ["enable_thinking"] = false },
            },
        };

        ChatResponse response;
        if (SystemPrompt is not null)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, userMessage),
            };
            response = await engine.GetResponseAsync(messages, options, cancellationToken: ct);
        }
        else
        {
            // Fallback for fine-tuned models that don't need a system prompt
            response = await engine.GetResponseAsync(userMessage, options, cancellationToken: ct);
        }

        var raw = response.Text ?? "";

        // Strip <think>...</think> blocks from reasoning models (Qwen3.5 thinking mode)
        if (raw.Contains("</think>"))
        {
            while (raw.Contains("<think>") && raw.Contains("</think>"))
            {
                var start = raw.IndexOf("<think>", StringComparison.Ordinal);
                var end = raw.IndexOf("</think>", StringComparison.Ordinal) + "</think>".Length;
                if (start >= 0 && end > start)
                    raw = (raw[..start] + raw[end..]).TrimStart();
                else
                    break;
            }
            if (raw.StartsWith("</think>"))
                raw = raw["</think>".Length..].TrimStart();
        }

        // 3. Parse + store: protocol first (to get ID), then link components
        var lines = BioChainParser.Parse(raw);
        var stored = 0;

        foreach (var line in lines)
        {
            // v5.1: save protocol first to get its ID
            var protocolEntity = new ProtocolEntity
            {
                SubjectId = subjectId,
                Tag = line.Tag,
                Formula = line.Formula,
                Status = line.Status,
                Phase = line.Phase,
                Seq = stored,
                StimuliId = entry.Id,
            };

            // BIND/FAIL store their formula directly on the protocol
            if (line.Tag is "BIND")
                protocolEntity.BindExpr = line.Formula;
            else if (line.Tag is "FAIL")
                protocolEntity.FailCondition = line.Formula;

            var protocol = await protocols.CreateAsync(protocolEntity, ct);

            // Link components — they reference protocol via protocol_id
            await LinkComponentAsync(protocol, line, subjectId, ct);
            stored++;
        }

        // 4. Mark analyzed
        await stimuli.MarkAnalyzedAsync(entry.Id, ct);

        return new AnalyzeResult(entry.Id, stored, lines.Count, raw);
    }

    private async Task LinkComponentAsync(ProtocolEntity protocol, BioChainParser.ParsedLine line,
        Guid subjectId, CancellationToken ct)
    {
        switch (line.Tag)
        {
            case "SIGNAL":
            case "STATE":
            {
                var sig = BioChainParser.ExtractSignal(line.Formula);
                if (sig is null) break;

                // Resolve region code -> region ID
                int? regionId = null;
                if (sig.Value.Region is not null)
                {
                    var region = await regions.GetOrCreateAsync(sig.Value.Region, subjectId, ct);
                    regionId = region.Id;
                }

                var tau = _tauLookup.Value.GetValueOrDefault(sig.Value.Code);
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
                // LLM_GATE can be a single-line gate or a multi-line block:
                // Single-line: {⊨(FEAR@MKT > baseline)}
                // Block: name { PROMPT: "..." MODEL: ... PARSE: ... FALLBACK: ... TIMEOUT: N CACHE: N }
                var gateEntity = new GateEntity
                {
                    SubjectId = subjectId,
                    Type = "llm",
                    ProtocolId = protocol.Id,
                };

                if (line.Formula.Contains("PROMPT:") || line.Formula.Contains("MODEL:") ||
                    line.Formula.Contains("FALLBACK:"))
                {
                    // Block format — parse structured fields
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
                    // Single-line format — try standard gate extraction
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

                // Resolve region codes -> region IDs
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
                // TOOL can be a multi-line block:
                // name { INPUT: refs INVOKE: "endpoint" OUTPUT: refs GATE: expr TIMEOUT: N RETRY: N FALLBACK: expr }
                var toolEntity = new ToolEntity
                {
                    SubjectId = subjectId,
                    ProtocolId = protocol.Id,
                };

                if (line.Formula.Contains("INVOKE:") || line.Formula.Contains("INPUT:") ||
                    line.Formula.Contains("OUTPUT:"))
                {
                    // Block format — parse structured fields
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

                    // Default invoke to code if not specified
                    if (string.IsNullOrEmpty(toolEntity.Invoke))
                        toolEntity.Invoke = toolEntity.Code;
                }
                else
                {
                    // Single-line format
                    var code = line.Formula.Trim();
                    toolEntity.Code = code.Length > 50 ? code[..50] : code;
                    toolEntity.Invoke = line.Formula;
                }

                await tools.CreateAsync(toolEntity, ct);
                break;
            }

            case "MODULE":
            {
                // MODULE can be multi-line block: name { DEF: ... IMPORT: ... }
                var moduleEntity = new ModuleEntity { SubjectId = subjectId };
                if (line.Formula.Contains('{'))
                {
                    var blockLines = line.Formula.Split('\n');
                    var name = blockLines[0].Replace("{", "").Trim();
                    moduleEntity.Code = (name.Length > 50 ? name[..50] : name).Length > 0
                        ? (name.Length > 50 ? name[..50] : name) : "module";
                    // Store block body in properties as JSON
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
                        moduleEntity.Properties = System.Text.Json.JsonSerializer.Serialize(bodyParts);
                }
                else
                {
                    // Single-line: just use formula as code (truncated)
                    var code = line.Formula.Trim();
                    moduleEntity.Code = code.Length > 50 ? code[..50] : code;
                }
                await modules.CreateAsync(moduleEntity, ct);
                break;
            }

            case "BIND":
            case "FAIL":
                // Handled in the main loop before protocol creation
                break;

            case "FORMULA":
            case "FEEDBACK":
            case "DEF":
            {
                // Extract gate condition suffix {|=(...)} if present
                var (gateInfo, cleanFormula) = BioChainParser.ExtractFormulaGateCondition(line.Formula);

                // Extract signal refs from the cleaned formula
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

                // Create edge if both source and target signals exist
                if (srcSignal is not null && tgtSignal is not null)
                {
                    var opClass = line.Tag == "FEEDBACK" ? "feedback" : "causal";
                    var op = line.Tag == "FEEDBACK" ? "\u27f3\u207b" : "\u2192"; // ⟳⁻ or ->

                    // Create gate entity if gate condition was found
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
            {
                // Predictions/hypotheses/interventions stored as protocols — no additional entity needed.
                // The evolution loop queries predictions via GetByModuleTagAsync.
                break;
            }

            case "DYSREG":
            {
                await stimuli.CreateAsync(new StimuliEntity
                    { SubjectId = subjectId, Kind = "inferred", SourceText = line.Formula, Analyzed = true }, ct);

                // Extract gate condition suffix if present
                var (dysregGateInfo, dysregCleanFormula) = BioChainParser.ExtractFormulaGateCondition(line.Formula);

                // Create dysreg edge if signal refs can be extracted
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
                            Operator = "\u26a1", // ⚡
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

    /// <summary>
    /// Public entry point for the agent ecosystem to route parsed DSL lines
    /// through the same entity creation pipeline used by initial analysis.
    /// </summary>
    public Task LinkComponentPublicAsync(
        ProtocolEntity protocol, BioChainParser.ParsedLine line, Guid subjectId, CancellationToken ct)
        => LinkComponentAsync(protocol, line, subjectId, ct);

    private static string BuildPrompt(string text, string kind)
    {
        var preamble = Preambles.GetValueOrDefault(kind, "Analyze this text:");
        return $"{preamble}\n\n{text}";
    }

    /// <summary>
    /// Load the analyzer system prompt.
    /// Searches for SIGNALS_ANALYZER_PROMPT.txt first, then falls back to BIOCHAIN_ANALYZER_PROMPT.txt.
    /// Returns null if neither file found (fine-tuned models don't need it).
    /// </summary>
    private static string? LoadSystemPrompt()
    {
        var fileNames = new[] { "SIGNALS_ANALYZER_PROMPT.txt", "BIOCHAIN_ANALYZER_PROMPT.txt" };

        foreach (var fileName in fileNames)
        {
            // Walk up from bin dir to find the Data folder
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Data", fileName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Libraries",
                    "BioChain.Repository", "Data", fileName),
            };

            foreach (var path in candidates)
            {
                var full = Path.GetFullPath(path);
                if (!File.Exists(full)) continue;
                Console.WriteLine($"[AnalyzeService] Loaded system prompt from {full}");
                return File.ReadAllText(full).Trim();
            }
        }

        Console.WriteLine("[AnalyzeService] No analyzer prompt file found — using fine-tuned mode (no system prompt)");
        return null;
    }
}

public record AnalyzeResult(int StimuliId, int ProtocolsStored, int LinesTotal, string RawOutput);
