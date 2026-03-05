using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using BioChain.Kernel.Signals;

namespace BioChain.Server.Api;

public static class KernelApi
{
    public static RouteGroupBuilder MapKernelApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kernel").WithTags("Kernel");

        // ── POST /compile — BNF source → SQL + topo sort ──────────────────
        group.MapPost("/compile", (CompileRequest req) =>
        {
            try
            {
                IVocabulary? vocab = req.Vocabulary?.ToLowerInvariant() switch
                {
                    "bio" => Vocabulary.Bio,
                    "mkt" => Vocabulary.Market,
                    "game" => Vocabulary.Game,
                    "org" => Vocabulary.Org,
                    "soc" => Vocabulary.Social,
                    _ => null
                };

                var subjectId = req.SubjectId ?? Guid.NewGuid();
                var result = SignalsCompiler.Compile(req.Source, subjectId, vocab);

                return Results.Ok(new
                {
                    subjectId,
                    sqlStatements = result.SqlStatements,
                    topoLevels = result.TopoLevels,
                    warnings = result.Warnings,
                    signalCount = result.SqlStatements.Count(s => s.StartsWith("INSERT INTO signal")),
                    edgeCount = result.SqlStatements.Count(s => s.StartsWith("INSERT INTO edge")),
                });
            }
            catch (FormatException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ── POST /simulate — in-memory tick simulation ────────────────────
        group.MapPost("/simulate", (SimulateRequest req) =>
        {
            try
            {
                // Build SignalRow[] from request
                var signals = req.Signals.Select((s, i) => new SignalRow(
                    i, s.Code, null, "≈",
                    s.Value, s.Baseline ?? 0, s.Confidence ?? 1.0, "N",
                    s.TauMinMs ?? 0, 0,
                    s.RangeLow ?? 0, s.RangeHigh ?? 999999
                )).ToArray();

                // Build EdgeRow[] from request
                var edges = (req.Edges ?? []).Select((e, i) => new EdgeRow(
                    i, e.SourceIdx, e.TargetIdx,
                    e.Operator ?? "\u2192", e.OperatorClass ?? "causal",
                    e.Gain ?? 1.0, e.NoiseSigma ?? 0, e.TransferFn ?? "lin",
                    e.DelayMs ?? 0, e.ClampLo, e.ClampHi, e.GateId, null, true
                )).ToArray();

                // Build GateRow[] from request
                var gates = (req.Gates ?? []).Select(g => new GateRow(
                    g.Id, g.Code, g.Type ?? "threshold",
                    g.Threshold, g.Expression, null, false,
                    null, null, null, null, null, null
                )).ToArray();

                // Topo sort
                var topoLevels = GraphUtils.ComputeTopoLevels(signals.Length, edges);

                // Build context
                var ctx = new TickCtx
                {
                    Signals = new SignalColumns(signals),
                    Edges = edges,
                    Gates = gates,
                    TopoLevels = topoLevels,
                    TickIntervalMs = req.TickIntervalMs ?? 100,
                };

                // Build initial inputs from inject
                var inputs = (req.Inject ?? [])
                    .Select(inj => (Input)new Input.Inject(inj.SignalCode, inj.Value, inj.Confidence ?? 1.0))
                    .ToList();

                // Run ticks
                var ticks = Math.Clamp(req.Ticks ?? 1, 1, 1000);
                var allEvents = new List<object>();
                TickResult? lastResult = null;

                for (int t = 0; t < ticks; t++)
                {
                    var tickInputs = t == 0 ? inputs : [];
                    lastResult = TickPipeline.Run(ctx, tickInputs);

                    foreach (var evt in lastResult.Events)
                        allEvents.Add(new { tick = t + 1, type = evt.GetType().Name, detail = evt.ToString() });

                    if (lastResult.Stable) break;
                }

                // Build final state
                var finalState = new Dictionary<string, double>();
                for (int i = 0; i < ctx.Signals.Count; i++)
                    finalState[ctx.Signals.Codes[i]] = ctx.Signals.Values[i];

                return Results.Ok(new
                {
                    finalState,
                    events = allEvents,
                    ticksRun = lastResult?.TickNumber ?? 0,
                    stable = lastResult?.Stable ?? true,
                    cascadeDepth = lastResult?.CascadeDepth ?? 0,
                    protocols = lastResult?.Protocol.Select(p => new { p.Tag, p.Code, p.Content }) ?? [],
                    pendingSideEffects = lastResult?.Pending.Length ?? 0,
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ── POST /vm — FormulaVM bytecode execution ───────────────────────
        group.MapPost("/vm", (VmRequest req) =>
        {
            try
            {
                // Build signal columns
                var signals = req.Signals.Select((s, i) => new SignalRow(
                    i, s.Code, null, "≈", s.Value, 0, 1, "N", 0, 0, 0, 999999
                )).ToArray();
                var cols = new SignalColumns(signals);

                // Assemble bytecode from human-readable program
                using var ms = new MemoryStream();
                foreach (var instr in req.Program)
                {
                    var op = Enum.Parse<Op>(instr.Op, ignoreCase: true);
                    ms.WriteByte((byte)op);

                    switch (op)
                    {
                        case Op.Push:
                            ms.Write(BitConverter.GetBytes(instr.Operand ?? 0));
                            break;
                        case Op.Load or Op.Store or Op.Call:
                            ms.Write(BitConverter.GetBytes((int)(instr.Operand ?? 0)));
                            break;
                    }
                }

                var result = FormulaVM.Execute(ms.ToArray(), cols);

                // Return result + final signal values
                var signalState = new Dictionary<string, double>();
                for (int i = 0; i < cols.Count; i++)
                    signalState[cols.Codes[i]] = cols.Values[i];

                return Results.Ok(new { result, signals = signalState });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ── POST /embody — graph-driven LLM embodiment ─────────────────
        group.MapPost("/embody", async (
            EmbodyRequest req,
            [FromKeyedServices("chat")] IChatClient chat) =>
        {
            try
            {
                var systemPrompt = BuildEmbodyPrompt(req.Source, req.Context);

                var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };

                if (req.History is not null)
                    foreach (var h in req.History)
                        messages.Add(new ChatMessage(
                            h.Role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                            h.Content));

                messages.Add(new ChatMessage(ChatRole.User, req.Message));

                var options = new ChatOptions
                {
                    Temperature = 0.7f,
                    TopP = 0.8f,
                    TopK = 20,
                    PresencePenalty = 1.5f,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["chat_template_kwargs"] = new Dictionary<string, object>
                            { ["enable_thinking"] = false },
                    },
                };

                var response = await chat.GetResponseAsync(messages, options);
                var text = StripThinkBlocks(response.Text ?? "");

                return Results.Ok(new EmbodyResponse(text));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return group;
    }

    // ── Embody helpers ───────────────────────────────────────────────────────

    private const string EmbodyPreamble = """
        You ARE a living being. Your nervous system is defined by the BioChain Signal Graph below.
        You do not analyze it — you ARE it.

        # BioChain BNF Notation
        SIGNAL: declares a signal (neurotransmitter, hormone, peptide, behavioral state)
        FORMULA: declares a causal edge between signals
        Operators: → (causes/activates), ⊣ (inhibits/suppresses), ~ (modulates)
        Gate symbols: ⊨(threshold) ⊡(latch) ⊛(novelty) Σ(integrator) ⊳(gain) ¬(not)
        Feedback: ⟳⁻(negative/stabilizing loop) ⟳⁺(positive/amplifying loop)
        Layers: NT(neurotransmitter) H(hormone) P(peptide) eCB(endocannabinoid) NI(neuroimmune) NS(neurosteroid)
        """;

    private const string EmbodyRules = """

        # Rules
        - NEVER mention signal names, graph notation, biochemical terms, or BNF in your response
        - NEVER narrate what your nervous system is doing
        - Just FEEL the cascades internally and respond as a normal human would
        - Your graph shapes HOW you react — your words should be natural speech, not science
        """;

    private static string BuildEmbodyPrompt(string source, string context) =>
        $"{EmbodyPreamble}\n# Your Signal Graph\n{source}\n\n# Current Context\n{context}\n{EmbodyRules}";

    private static string StripThinkBlocks(string text)
    {
        while (text.Contains("<think>") && text.Contains("</think>"))
        {
            var start = text.IndexOf("<think>", StringComparison.Ordinal);
            var end = text.IndexOf("</think>", StringComparison.Ordinal) + "</think>".Length;
            text = string.Concat(text.AsSpan(0, start), text.AsSpan(end));
        }
        return text.StartsWith("</think>") ? text["</think>".Length..].TrimStart() : text.Trim();
    }

}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CompileRequest(string Source, string? Vocabulary = null, Guid? SubjectId = null);

public record SimulateRequest(
    SimSignalDef[] Signals,
    SimEdgeDef[]? Edges = null,
    SimGateDef[]? Gates = null,
    SimInject[]? Inject = null,
    int? Ticks = 1,
    double? TickIntervalMs = 100);

public record SimSignalDef(string Code, double Value, double? Baseline = null,
    double? Confidence = null, double? TauMinMs = null,
    double? RangeLow = null, double? RangeHigh = null);

public record SimEdgeDef(int SourceIdx, int TargetIdx,
    string? Operator = null, string? OperatorClass = null,
    double? Gain = null, double? NoiseSigma = null, string? TransferFn = null,
    int? DelayMs = null, double? ClampLo = null, double? ClampHi = null, int? GateId = null);

public record SimGateDef(int Id, string Code, string? Type = null,
    double? Threshold = null, string? Expression = null);

public record SimInject(string SignalCode, double Value, double? Confidence = null);

public record VmRequest(VmSignalDef[] Signals, VmInstruction[] Program);
public record VmSignalDef(string Code, double Value);
public record VmInstruction(string Op, double? Operand = null);

public record EmbodyRequest(
    string Source,
    string Context,
    string Message,
    List<EmbodyHistoryItem>? History = null);
public record EmbodyHistoryItem(string Role, string Content);
public record EmbodyResponse(string Response);
