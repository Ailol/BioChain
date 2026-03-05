using System.Collections.Concurrent;
using System.Globalization;

namespace BioChain.Kernel.Signals;

// ──────────────────────── TICK PIPELINE ────────────────────────

public static class TickPipeline
{
    private const int MaxCascade = 8;

    public static TickResult Run(TickCtx ctx, IReadOnlyList<Input> inputs)
    {
        ctx.TickNumber++;
        ctx.CascadeDepth = 0;
        ctx.Stable = false;
        ctx.Events.Clear();
        ctx.Pending.Clear();
        ctx.Protocol.Clear();

        // Phase 1: Resolve — apply external inputs
        ResolvePhase(ctx, inputs);

        // Phase 2: Decay — tau-based exponential decay (source-only signals)
        DecayPhase(ctx);

        // Snapshot post-injection/decay state for temporal blend
        var preValues = (double[])ctx.Signals.Values.Clone();

        // Cascade loop — converges to instantaneous equilibrium within this tick
        var changed = true;
        while (changed && ctx.CascadeDepth < MaxCascade)
        {
            // Phase 3: Formula — evaluate FormulaVM bytecode (future)
            FormulaPhase(ctx);

            // Phase 4: Propagate — topo sort + edge transforms
            changed = PropagatePhase(ctx);

            // Phase 5: Gate — evaluate gates, queue LLM_GATE as SideEffect
            GatePhase(ctx);

            if (changed) ctx.CascadeDepth++;
        }

        // Phase 5b: Temporal integration — blend pre-tick state toward cascade result
        TemporalBlendPhase(ctx, preValues);

        // Phase 6: Constrain — boundary/simultaneous/equilibrium/conserve
        ConstrainPhase(ctx);

        // Phase 7: Fail — check FAIL conditions
        FailPhase(ctx);

        // Phase 8: Bind — evaluate BIND expressions
        BindPhase(ctx);

        // Phase 9: Emit — finalize
        EmitPhase(ctx);

        return new TickResult(
            ctx.Signals.ToRecordBatch(),
            [.. ctx.Protocol],
            [.. ctx.Pending],
            [.. ctx.Events],
            ctx.Stable,
            ctx.CascadeDepth,
            ctx.TickNumber);
    }

    // ── Phase 1: Resolve ──
    private static void ResolvePhase(TickCtx ctx, IReadOnlyList<Input> inputs)
    {
        foreach (var input in inputs)
        {
            switch (input)
            {
                case Input.Inject inj:
                    if (ctx.Signals.Index.TryGetValue(inj.SignalCode, out var idx))
                    {
                        var old = ctx.Signals.Values[idx];
                        ctx.Signals.Values[idx] = inj.Value;
                        ctx.Signals.Confidences[idx] = inj.Confidence;
                        ctx.Events.Add(new KernelEvt.SignalChange(inj.SignalCode, old, inj.Value, inj.Confidence));
                    }
                    break;
                case Input.ToolResult tool:
                    foreach (var (code, val) in tool.Outputs)
                        if (ctx.Signals.Index.TryGetValue(code, out var ti))
                        {
                            var old = ctx.Signals.Values[ti];
                            ctx.Signals.Values[ti] = val;
                            ctx.Events.Add(new KernelEvt.SignalChange(code, old, val, 1.0));
                        }
                    break;
            }
        }

        // Apply resolved side effects from last tick
        foreach (var resolved in ctx.ResolvedInputs) ResolvePhase(ctx, [resolved]);
        ctx.ResolvedInputs.Clear();
    }

    // ── Phase 2: Decay ──
    // Only applies tau-based exponential decay to SOURCE-ONLY signals (no inbound edges).
    // Signals with inbound edges get their homeostasis from gLeak in the conductance model,
    // preventing double-homeostasis that collapses all deviations.
    private static void DecayPhase(TickCtx ctx)
    {
        // Build set of signals that have inbound edges (they use conductance model instead)
        var hasInbound = new bool[ctx.Signals.Count];
        foreach (var edge in ctx.Edges)
            if (edge.Active && edge.TargetId >= 0 && edge.TargetId < ctx.Signals.Count)
                hasInbound[edge.TargetId] = true;

        var vals = ctx.Signals.Values;
        var bases = ctx.Signals.Baselines;
        var taus = ctx.Signals.TauMinMs;
        var dt = ctx.TickIntervalMs;

        for (int i = 0; i < ctx.Signals.Count; i++)
        {
            if (hasInbound[i]) continue;  // conductance model handles these
            if (taus[i] <= 0) continue;
            var diff = vals[i] - bases[i];
            if (Math.Abs(diff) < 1e-10) continue;

            var factor = Math.Exp(-dt / taus[i]);
            var newVal = bases[i] + diff * factor;
            if (Math.Abs(newVal - bases[i]) < 1e-6) newVal = bases[i];

            if (Math.Abs(newVal - vals[i]) > 1e-10)
            {
                ctx.Events.Add(new KernelEvt.SignalChange(ctx.Signals.Codes[i], vals[i], newVal, ctx.Signals.Confidences[i]));
                vals[i] = newVal;
            }
        }
    }

    // ── Phase 3: Formula — evaluate LLM-defined expressions ──
    private static void FormulaPhase(TickCtx ctx)
    {
        foreach (var formula in ctx.Formulas)
        {
            var computed = SimpleFormula.Evaluate(formula.Expression, ctx.Signals, ctx.FormulaOutputs);

            if (formula.DecayRate >= 1.0)
            {
                ctx.FormulaOutputs[formula.OutputCode] = computed;
            }
            else
            {
                var prev = ctx.FormulaOutputs.GetValueOrDefault(formula.OutputCode, 0);
                ctx.FormulaOutputs[formula.OutputCode] =
                    prev * (1.0 - formula.DecayRate) + computed * formula.DecayRate;
            }
        }
    }

    // ── Phase 4: Propagate (conductance-based shunting inhibition + adaptation) ──
    //
    // Uses a conductance model inspired by Hodgkin-Huxley: each edge contributes
    // conductance to excitatory or inhibitory channels. The membrane equation
    // computes the new value from competing pulls toward reversal potentials.
    //
    private static bool PropagatePhase(TickCtx ctx)
    {
        var anyChanged = false;
        var vals = ctx.Signals.Values;

        // Lazy-init adaptation state
        if (ctx.EdgeGainMod is null)
        {
            ctx.EdgeGainMod = new double[ctx.Edges.Length];
            System.Array.Fill(ctx.EdgeGainMod, 1.0);
        }
        else
        {
            // Recovery: all edges slowly regain original gain (per cascade step)
            for (int ei = 0; ei < ctx.EdgeGainMod.Length; ei++)
                ctx.EdgeGainMod[ei] += (1.0 - ctx.EdgeGainMod[ei]) * 0.02;
        }

        // Pre-build inbound edge index: O(edges) instead of O(nodes × edges)
        var inbound = new List<int>[ctx.Signals.Count];
        for (int i = 0; i < ctx.Signals.Count; i++) inbound[i] = [];
        for (int ei = 0; ei < ctx.Edges.Length; ei++)
        {
            var e = ctx.Edges[ei];
            if (e.Active && e.TargetId >= 0 && e.TargetId < ctx.Signals.Count)
                inbound[e.TargetId].Add(ei);
        }

        foreach (var level in ctx.TopoLevels)
        {
            foreach (var nodeIdx in level)
            {
                var edgeIndices = inbound[nodeIdx];
                if (edgeIndices.Count == 0) continue;

                var code = ctx.Signals.Codes[nodeIdx];

                // Reversal potentials for conductance model
                var eExc = ctx.Signals.RangeHigh[nodeIdx];
                var eInh = ctx.Signals.RangeLow[nodeIdx];
                var eRest = ctx.Signals.Baselines[nodeIdx];

                // Leak conductance proportional to signal kinetics
                var tau = ctx.Signals.TauMinMs[nodeIdx];
                var gLeak = 0.2 + 0.3 * Math.Exp(-tau / 50.0);

                // Collect conductances from inbound edges
                double gExc = 0, gInh = 0, gDepletion = 0;
                var blocked = false;

                foreach (var ei in edgeIndices)
                {
                    var edge = ctx.Edges[ei];
                    if (edge.SourceId < 0 || edge.SourceId >= vals.Length) continue;

                    var srcDeviation = vals[edge.SourceId] - ctx.Signals.Baselines[edge.SourceId];
                    var adaptedGain = edge.Gain * ctx.EdgeGainMod[ei];
                    var raw = srcDeviation * adaptedGain;

                    // Transfer function
                    raw = edge.TransferFn switch
                    {
                        "log" => Math.Sign(raw) * Math.Log(1.0 + Math.Abs(raw)),
                        "exp" => Math.Exp(Math.Clamp(raw, -20, 20)),
                        "sig" => 2.0 / (1.0 + Math.Exp(-raw)) - 1.0,
                        "step" => raw >= 0 ? 1.0 : -1.0,
                        "relu" => Math.Max(0, raw),
                        _ => raw // "lin"
                    };

                    // Edge clamps
                    if (edge.ClampLo.HasValue) raw = Math.Max(raw, edge.ClampLo.Value);
                    if (edge.ClampHi.HasValue) raw = Math.Min(raw, edge.ClampHi.Value);

                    // Route to conductance channel based on operator
                    switch (edge.Operator)
                    {
                        case "\u22A3":  // ⊣ inhibitory
                            gInh += Math.Abs(raw);
                            break;
                        case "\u2297":  // ⊗ block
                            if (Math.Abs(srcDeviation) > 0.01) blocked = true;
                            break;
                        case "\u2296\u2192":  // ⊖→ depletory
                            gDepletion += Math.Abs(raw);
                            break;
                        default:        // → and all excitatory operators
                            if (raw >= 0)
                                gExc += raw;
                            else
                                gInh += Math.Abs(raw);
                            break;
                    }

                    // Adaptation: active edges desensitize (receptor downregulation)
                    if (Math.Abs(raw) > 0.01)
                    {
                        var rate = InferAdaptation(edge, ctx.Signals);
                        ctx.EdgeGainMod[ei] *= 1.0 - rate * Math.Min(Math.Abs(raw), 1.0);
                        ctx.EdgeGainMod[ei] = Math.Max(ctx.EdgeGainMod[ei], 0.1);
                    }
                }

                // Conductance-based membrane equation (Hodgkin-Huxley inspired)
                double newVal;
                if (blocked)
                {
                    newVal = eRest;
                }
                else
                {
                    var gTotal = gExc + gInh + gLeak + gDepletion;
                    var vEq = gTotal > 1e-10
                        ? (gExc * eExc + gInh * eInh + (gLeak + gDepletion) * eRest) / gTotal
                        : eRest;

                    var rate = gTotal / (gTotal + 0.5);
                    newVal = vals[nodeIdx] + (vEq - vals[nodeIdx]) * rate;
                }

                if (Math.Abs(newVal - vals[nodeIdx]) > 1e-10)
                {
                    ctx.Events.Add(new KernelEvt.SignalChange(code, vals[nodeIdx], newVal,
                        ctx.Signals.Confidences[nodeIdx]));
                    vals[nodeIdx] = newVal;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged) ctx.Events.Add(new KernelEvt.CascadeStep(ctx.CascadeDepth));
        return anyChanged;

        static double InferAdaptation(EdgeRow edge, SignalColumns signals)
        {
            var opClass = edge.OperatorClass.ToLowerInvariant();
            var baseRate = opClass switch
            {
                _ when opClass.Contains("receptor") => 0.10,
                _ when opClass.Contains("channel") => 0.08,
                _ when opClass.Contains("transporter") => 0.06,
                _ when opClass.Contains("modulator") => 0.04,
                _ when opClass.Contains("enzyme") => 0.03,
                _ => 0.05
            };

            var targetTau = edge.TargetId < signals.TauMinMs.Length
                ? signals.TauMinMs[edge.TargetId] : 0;
            var tauFactor = targetTau > 0
                ? 1.0 / (1.0 + targetTau / 500.0)
                : 0.5;

            var gainFactor = Math.Min(Math.Abs(edge.Gain), 2.0) / 2.0;

            return baseRate * (0.5 + tauFactor) * (0.5 + gainFactor);
        }
    }

    // ── Phase 5: Gate ──
    private static void GatePhase(TickCtx ctx)
    {
        foreach (var gate in ctx.Gates)
        {
            switch (gate.Type)
            {
                case "threshold":
                    var fired = gate.Threshold is null || true;
                    ctx.Events.Add(fired ? new KernelEvt.GateFire(gate.Id, gate.Type) : new KernelEvt.GateBlock(gate.Id, gate.Type));
                    SetGatedEdges(ctx.Edges, gate.Id, fired);
                    break;

                case "latch":
                    var latchFired = gate.Latched || true;
                    ctx.Events.Add(new KernelEvt.GateFire(gate.Id, gate.Type));
                    SetGatedEdges(ctx.Edges, gate.Id, latchFired);
                    break;

                case "and" or "or" or "not" or "xor" or "splitter":
                    ctx.Events.Add(new KernelEvt.GateFire(gate.Id, gate.Type));
                    SetGatedEdges(ctx.Edges, gate.Id, true);
                    break;

                case "llm":
                    ctx.Pending.Add(new SideEffect.LlmGate(
                        gate.Id, gate.Prompt ?? "", gate.Model ?? "default",
                        gate.ParseMap, gate.Fallback, gate.TimeoutMs ?? 30000, gate.CacheMs > 0));
                    if (gate.Fallback is not null)
                        SetGatedEdges(ctx.Edges, gate.Id, gate.Fallback != "false");
                    break;

                case "integrator" or "novelty" or "gain":
                    break;
            }
        }
    }

    private static void SetGatedEdges(EdgeRow[] edges, int gateId, bool active)
    {
        for (int i = 0; i < edges.Length; i++)
            if (edges[i].GateId == gateId)
                edges[i] = edges[i] with { Active = active };
    }

    // ── Phase 6: Constrain ──
    private static void ConstrainPhase(TickCtx ctx)
    {
        var vals = ctx.Signals.Values;
        var lo = ctx.Signals.RangeLow;
        var hi = ctx.Signals.RangeHigh;

        for (int i = 0; i < ctx.Signals.Count; i++)
        {
            if (lo[i] > 0 && vals[i] < lo[i]) { vals[i] = lo[i]; }
            if (hi[i] < double.MaxValue && vals[i] > hi[i]) { vals[i] = hi[i]; }
        }
    }

    // ── Phase 7: Fail ──
    private static void FailPhase(TickCtx ctx)
    {
        // Placeholder — will be populated from compiled FailDecl AST nodes
    }

    // ── Phase 8: Bind ──
    private static void BindPhase(TickCtx ctx)
    {
        foreach (var (code, value) in ctx.FormulaOutputs)
        {
            ctx.Protocol.Add(new ProtocolEntry("formula", code,
                $"{code} = {value:F4}", null));
        }
    }

    // ── Phase 5b: Temporal Integration ──
    private static void TemporalBlendPhase(TickCtx ctx, double[] preValues)
    {
        var vals = ctx.Signals.Values;
        var dt = ctx.TickIntervalMs;

        for (int i = 0; i < ctx.Signals.Count; i++)
        {
            var tau = ctx.Signals.TauMinMs[i];
            if (tau <= 0) continue;

            var cascadeTarget = vals[i];
            var before = preValues[i];

            if (Math.Abs(cascadeTarget - before) < 1e-10) continue;

            var alpha = 1.0 - Math.Exp(-(double)dt / tau);
            vals[i] = before + (cascadeTarget - before) * alpha;
        }
    }

    // ── Phase 9: Emit ──
    private static void EmitPhase(TickCtx ctx)
    {
        ctx.Stable = ctx.Events.Count == 0;
        if (ctx.Stable)
            ctx.Events.Add(new KernelEvt.EvalStable(ctx.TickNumber));
    }
}

// ──────────────────────── GRAPH UTILS ────────────────────────

public static class GraphUtils
{
    /// <summary>
    /// Kahn's algorithm: compute topological levels from signal count + edges.
    /// Returns int[][] where each level contains signal indices that can be evaluated in parallel.
    /// Nodes in cycles are appended as a final "cycle" level.
    /// </summary>
    public static int[][] ComputeTopoLevels(int signalCount, EdgeRow[] edges)
    {
        var inDeg = new int[signalCount];
        var adj = Enumerable.Range(0, signalCount).Select(_ => new List<int>()).ToArray();

        foreach (var e in edges)
        {
            if (e.SourceId >= 0 && e.SourceId < signalCount && e.TargetId >= 0 && e.TargetId < signalCount)
            {
                adj[e.SourceId].Add(e.TargetId);
                inDeg[e.TargetId]++;
            }
        }

        var levels = new List<int[]>();
        var visited = new bool[signalCount];
        var queue = Enumerable.Range(0, signalCount).Where(i => inDeg[i] == 0).ToList();
        while (queue.Count > 0)
        {
            levels.Add([.. queue]);
            foreach (var node in queue) visited[node] = true;
            var next = new List<int>();
            foreach (var node in queue)
                foreach (var m in adj[node])
                    if (--inDeg[m] == 0) next.Add(m);
            queue = next;
        }

        var cyclic = Enumerable.Range(0, signalCount).Where(i => !visited[i]).ToArray();
        if (cyclic.Length > 0)
            levels.Add(cyclic);

        return [.. levels];
    }
}

// ──────────────────────── FORMULA VM ────────────────────────

public enum Op : byte
{
    Nop, Push, Pop, Load, Store,          // stack + signal access
    Add, Sub, Mul, Div, Mod, Neg,         // arithmetic
    Gt, Lt, Eq, And, Or,                  // comparison + logic
    Call                                   // host function call (Extism or native)
}

public static class FormulaVM
{
    /// <summary>
    /// Execute bytecode against signal columns. Stack-based interpreter.
    /// Each opcode is 1 byte + optional operand (8 bytes double or 4 bytes int).
    /// </summary>
    public static double Execute(ReadOnlySpan<byte> bytecode, SignalColumns signals)
    {
        var stack = new Stack<double>(16);
        int ip = 0;

        while (ip < bytecode.Length)
        {
            var op = (Op)bytecode[ip++];
            switch (op)
            {
                case Op.Nop: break;

                case Op.Push:
                    stack.Push(BitConverter.ToDouble(bytecode.Slice(ip, 8)));
                    ip += 8;
                    break;

                case Op.Pop:
                    stack.Pop();
                    break;

                case Op.Load:
                    var loadIdx = BitConverter.ToInt32(bytecode.Slice(ip, 4));
                    ip += 4;
                    stack.Push(loadIdx >= 0 && loadIdx < signals.Count ? signals.Values[loadIdx] : 0);
                    break;

                case Op.Store:
                    var storeIdx = BitConverter.ToInt32(bytecode.Slice(ip, 4));
                    ip += 4;
                    if (storeIdx >= 0 && storeIdx < signals.Count)
                        signals.Values[storeIdx] = stack.Pop();
                    break;

                case Op.Add: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a + b); break; }
                case Op.Sub: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a - b); break; }
                case Op.Mul: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a * b); break; }
                case Op.Div: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(b != 0 ? a / b : 0); break; }
                case Op.Mod: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(b != 0 ? a % b : 0); break; }
                case Op.Neg: stack.Push(-stack.Pop()); break;

                case Op.Gt: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a > b ? 1 : 0); break; }
                case Op.Lt: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a < b ? 1 : 0); break; }
                case Op.Eq: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(Math.Abs(a - b) < 1e-10 ? 1 : 0); break; }
                case Op.And: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a != 0 && b != 0 ? 1 : 0); break; }
                case Op.Or: { var b = stack.Pop(); var a = stack.Pop(); stack.Push(a != 0 || b != 0 ? 1 : 0); break; }

                case Op.Call:
                    var fnId = BitConverter.ToInt32(bytecode.Slice(ip, 4));
                    ip += 4;
                    // Route to ExtismHost for WASM functions, or native builtins
                    stack.Push(ExtismHost.CallFunction(fnId, stack));
                    break;
            }
        }

        return stack.Count > 0 ? stack.Pop() : 0;
    }
}

// ──────────────────────── SIMPLE FORMULA EVALUATOR ────────────────────────

/// <summary>
/// Minimal infix expression evaluator for LLM-created formulas.
/// Supports: signal codes (DA, 5HT, CORT@ADR), numbers, +, -, *, /, (, ), unary minus.
/// Signal codes resolve to current values from SignalColumns or FormulaOutputs.
/// </summary>
public static class SimpleFormula
{
    public static double Evaluate(string expression, SignalColumns signals,
        Dictionary<string, double>? formulaOutputs = null)
    {
        var tokens = Tokenize(expression);
        var pos = 0;
        return ParseAddSub(tokens, ref pos, signals, formulaOutputs);
    }

    /// <summary>Extract all signal codes referenced in the expression.</summary>
    public static string[] ExtractCodes(string expression)
    {
        var tokens = Tokenize(expression);
        return tokens.Where(t => t.Type == FTok.Ident).Select(t => t.Text).Distinct().ToArray();
    }

    private static double ParseAddSub(List<FToken> tokens, ref int pos,
        SignalColumns signals, Dictionary<string, double>? fo)
    {
        var left = ParseMulDiv(tokens, ref pos, signals, fo);
        while (pos < tokens.Count && tokens[pos].Type is FTok.Plus or FTok.Minus)
        {
            var op = tokens[pos++].Type;
            var right = ParseMulDiv(tokens, ref pos, signals, fo);
            left = op == FTok.Plus ? left + right : left - right;
        }
        return left;
    }

    private static double ParseMulDiv(List<FToken> tokens, ref int pos,
        SignalColumns signals, Dictionary<string, double>? fo)
    {
        var left = ParseUnary(tokens, ref pos, signals, fo);
        while (pos < tokens.Count && tokens[pos].Type is FTok.Star or FTok.Slash)
        {
            var op = tokens[pos++].Type;
            var right = ParseUnary(tokens, ref pos, signals, fo);
            left = op == FTok.Star ? left * right : (right != 0 ? left / right : 0);
        }
        return left;
    }

    private static double ParseUnary(List<FToken> tokens, ref int pos,
        SignalColumns signals, Dictionary<string, double>? fo)
    {
        if (pos < tokens.Count && tokens[pos].Type == FTok.Minus)
        {
            pos++;
            return -ParseAtom(tokens, ref pos, signals, fo);
        }
        return ParseAtom(tokens, ref pos, signals, fo);
    }

    private static double ParseAtom(List<FToken> tokens, ref int pos,
        SignalColumns signals, Dictionary<string, double>? fo)
    {
        if (pos >= tokens.Count) return 0;
        var tok = tokens[pos];
        switch (tok.Type)
        {
            case FTok.Number:
                pos++;
                return tok.NumValue;
            case FTok.Ident:
                pos++;
                if (signals.Index.TryGetValue(tok.Text, out var idx))
                    return signals.Values[idx];
                if (fo?.TryGetValue(tok.Text, out var fv) == true)
                    return fv;
                return 0;
            case FTok.LParen:
                pos++;
                var val = ParseAddSub(tokens, ref pos, signals, fo);
                if (pos < tokens.Count && tokens[pos].Type == FTok.RParen) pos++;
                return val;
            default:
                pos++;
                return 0;
        }
    }

    private static List<FToken> Tokenize(string expr)
    {
        var tokens = new List<FToken>();
        var i = 0;
        while (i < expr.Length)
        {
            var c = expr[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '+') { tokens.Add(new FToken(FTok.Plus)); i++; }
            else if (c == '-') { tokens.Add(new FToken(FTok.Minus)); i++; }
            else if (c == '*') { tokens.Add(new FToken(FTok.Star)); i++; }
            else if (c == '/') { tokens.Add(new FToken(FTok.Slash)); i++; }
            else if (c == '(') { tokens.Add(new FToken(FTok.LParen)); i++; }
            else if (c == ')') { tokens.Add(new FToken(FTok.RParen)); i++; }
            else if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                double.TryParse(expr[start..i], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var num);
                tokens.Add(new FToken(FTok.Number, num));
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] is '_' or '@')) i++;
                tokens.Add(new FToken(FTok.Ident, 0, expr[start..i]));
            }
            else i++;
        }
        return tokens;
    }

    // Private token types for SimpleFormula (distinct from DSL Lexer's Tok enum)
    private enum FTok { Number, Ident, Plus, Minus, Star, Slash, LParen, RParen }
    private readonly record struct FToken(FTok Type, double NumValue = 0, string Text = "");
}

// ──────────────────────── EXTISM HOST ────────────────────────

public static class ExtismHost
{
    internal static readonly ConcurrentDictionary<string, Extism.Sdk.Plugin> Plugins = new();
    private static readonly ConcurrentDictionary<int, (string PluginName, string FnName)> FnRegistry = new();
    private static int _nextFnId;

    /// <summary>Register a WASM plugin from file or URL.</summary>
    public static void RegisterPlugin(string name, string wasmPath)
    {
        var manifest = new Extism.Sdk.Manifest(new Extism.Sdk.PathWasmSource(wasmPath));
        var plugin = new Extism.Sdk.Plugin(manifest, [], withWasi: true);
        Plugins[name] = plugin;
    }

    /// <summary>Register a function from a loaded plugin for FormulaVM Call opcode.</summary>
    public static int RegisterFunction(string pluginName, string fnName)
    {
        var id = Interlocked.Increment(ref _nextFnId);
        FnRegistry[id] = (pluginName, fnName);
        return id;
    }

    /// <summary>Called by FormulaVM Op.Call — routes to WASM plugin function.</summary>
    public static double CallFunction(int fnId, Stack<double> stack)
    {
        if (!FnRegistry.TryGetValue(fnId, out var reg)) return 0;
        if (!Plugins.TryGetValue(reg.PluginName, out var plugin)) return 0;

        // Marshal: pop arg from stack as JSON string, call plugin, parse result
        var arg = stack.Count > 0 ? stack.Pop().ToString() : "0";
        var result = plugin.Call(reg.FnName, arg);
        return double.TryParse(result, out var val) ? val : 0;
    }

    /// <summary>Unload all plugins.</summary>
    public static void DisposeAll()
    {
        foreach (var p in Plugins.Values) p.Dispose();
        Plugins.Clear();
        FnRegistry.Clear();
    }
}
