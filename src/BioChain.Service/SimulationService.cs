using System.Text;
using BioChain.Kernel.Signals;
using BioChain.Repository.Data;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Service;

/// <summary>
/// Loads a subject's real biochemical graph from PostgreSQL and runs tick simulation
/// with injected signal changes. Returns compact deltas showing what changed.
/// </summary>
public class SimulationService(BioChainDbContext db)
{
    public async Task<SimulationResult> SimulateAsync(
        Guid entityId,
        List<(string SignalCode, double Value)> injections,
        int ticks = 5,
        List<FormulaRule>? formulas = null,
        CancellationToken ct = default)
    {
        ticks = Math.Clamp(ticks, 1, 20);
        var pid = entityId.ToString();

        // 1. Load signals from v_signal_current
        //    State → value fallback (deviation from baseline, -1 to +1):
        //    ↑↑→+0.8, ↑→+0.4, ≈→0.0, ↓→-0.4, ↓↓→-0.8, ~→-0.2, ⊘→-1.0
        var signalRows = await db.Database
            .SqlQueryRaw<PgSignalRow>("""
                SELECT s.id, s.code, r.code AS region, s.state,
                       COALESCE(s.value, CASE s.state
                           WHEN '↑↑' THEN 0.8 WHEN '↑' THEN 0.4 WHEN '≈' THEN 0.0
                           WHEN '↓' THEN -0.4 WHEN '↓↓' THEN -0.8
                           WHEN '~' THEN -0.2 WHEN '⊘' THEN -1.0
                           ELSE 0.0
                       END)::FLOAT8 AS value,
                       COALESCE(s.baseline, 0)::FLOAT8 AS baseline,
                       COALESCE(s.confidence, 1)::FLOAT8 AS confidence,
                       COALESCE(s.distribution, 'N') AS distribution,
                       COALESCE(s.tau_min_ms, 0)::FLOAT8 AS tau_min_ms,
                       COALESCE(s.tau_max_ms, 0)::FLOAT8 AS tau_max_ms,
                       COALESCE(s.range_low, -1)::FLOAT8 AS range_low,
                       COALESCE(s.range_high, 1)::FLOAT8 AS range_high
                FROM v_signal_current s
                LEFT JOIN v_region_current r ON s.region_id = r.id AND s.entity_id = r.entity_id
                WHERE s.entity_id = {0}::uuid
                ORDER BY s.code, r.code
                """, pid)
            .ToListAsync(ct);

        if (signalRows.Count == 0)
            return SimulationResult.Empty("No signals found for this subject.");

        // 2. Build code → array index mapping
        //    Full key = "CODE@REGION" for uniqueness (DA@VTA vs DA@PFC)
        //    Also keep plain code → first index for edge resolution
        var fullKeyToIndex = new Dictionary<string, int>(signalRows.Count, StringComparer.OrdinalIgnoreCase);
        var codeToIndex = new Dictionary<string, int>(signalRows.Count, StringComparer.OrdinalIgnoreCase);
        var signals = new SignalRow[signalRows.Count];
        for (var i = 0; i < signalRows.Count; i++)
        {
            var s = signalRows[i];
            var fullKey = s.region is not null ? $"{s.code}@{s.region}" : s.code;
            fullKeyToIndex[fullKey] = i;
            codeToIndex.TryAdd(s.code, i); // first occurrence wins for edge resolution
            signals[i] = new SignalRow(
                i, s.code, s.region, s.state,
                s.value, s.baseline, s.confidence, s.distribution,
                s.tau_min_ms, s.tau_max_ms, s.range_low, s.range_high);
        }

        // 3. Load edges from v_graph, resolve codes → indices
        await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW v_node", ct);

        var edgeRows = await db.Database
            .SqlQueryRaw<PgEdgeRow>("""
                SELECT source_code, target_code, operator, operator_class,
                       COALESCE(gain, 1)::FLOAT8 AS gain,
                       COALESCE(noise_sigma, 0)::FLOAT8 AS noise_sigma,
                       COALESCE(transfer_fn, 'lin') AS transfer_fn,
                       COALESCE(delay_ms, 0)::INT AS delay_ms,
                       clamp_lo::FLOAT8 AS clamp_lo,
                       clamp_hi::FLOAT8 AS clamp_hi,
                       gate_id,
                       COALESCE(gate_active, true) AS gate_active
                FROM v_graph
                WHERE entity_id = {0}::uuid
                """, pid)
            .ToListAsync(ct);

        var edges = new List<EdgeRow>();
        foreach (var e in edgeRows)
        {
            if (!codeToIndex.TryGetValue(e.source_code, out var srcIdx)) continue;
            if (!codeToIndex.TryGetValue(e.target_code, out var tgtIdx)) continue;

            edges.Add(new EdgeRow(
                edges.Count, srcIdx, tgtIdx,
                e.@operator, e.operator_class,
                e.gain, e.noise_sigma, e.transfer_fn,
                e.delay_ms, e.clamp_lo, e.clamp_hi,
                e.gate_id, null, e.gate_active));
        }

        // 4. Load gates
        var gateRows = await db.Database
            .SqlQueryRaw<PgGateRow>("""
                SELECT id, code, type, threshold::FLOAT8, expression, probability::FLOAT8,
                       latched, prompt, model, parse_map, fallback_expr,
                       timeout_ms, cache_ms
                FROM v_gate_current
                WHERE entity_id = {0}::uuid
                """, pid)
            .ToListAsync(ct);

        var gates = gateRows.Select(g => new GateRow(
            g.id, g.code, g.type,
            g.threshold, g.expression, g.probability, g.latched,
            g.prompt, g.model, g.parse_map, g.fallback_expr,
            g.timeout_ms, g.cache_ms)).ToArray();

        // 5. Compute topo levels + build tick context
        var edgeArr = edges.ToArray();
        var topoLevels = GraphUtils.ComputeTopoLevels(signals.Length, edgeArr);

        var ctx = new TickCtx
        {
            Signals = new SignalColumns(signals),
            Edges = edgeArr,
            Gates = gates,
            TopoLevels = topoLevels,
            TickIntervalMs = 100,
        };

        // Register LLM-defined formulas
        if (formulas is { Count: > 0 })
            ctx.Formulas.AddRange(formulas);

        // Snapshot before-values
        var before = new double[ctx.Signals.Count];
        Array.Copy(ctx.Signals.Values, before, before.Length);

        // 6. Build injections — accept "DA" or "DA@VTA" format
        var inputs = new List<Input>();
        var unknownCodes = new List<string>();
        foreach (var (code, value) in injections)
        {
            if (fullKeyToIndex.TryGetValue(code, out var fIdx))
                inputs.Add(new Input.Inject(ctx.Signals.Codes[fIdx], value));
            else if (codeToIndex.TryGetValue(code, out var cIdx))
                inputs.Add(new Input.Inject(ctx.Signals.Codes[cIdx], value));
            else
                unknownCodes.Add(code);
        }

        // 7. Run simulation — capture full cascade narrative
        TickResult? lastResult = null;
        var gatesFired = new HashSet<string>();
        var gatesBlocked = new HashSet<string>();
        var cascadeSteps = new List<CascadeEvent>();

        // Track per-signal trajectory (value at each tick)
        var trajectories = new Dictionary<string, List<double>>();
        for (var i = 0; i < ctx.Signals.Count; i++)
            trajectories[ctx.Signals.Codes[i]] = [before[i]];

        // Build edge lookup: target signal code → list of source codes + operator
        var edgeLookup = new Dictionary<string, List<string>>();
        foreach (var e in edgeArr)
        {
            var tgtCode = ctx.Signals.Codes[e.TargetId];
            var srcCode = ctx.Signals.Codes[e.SourceId];
            if (!edgeLookup.ContainsKey(tgtCode))
                edgeLookup[tgtCode] = [];
            edgeLookup[tgtCode].Add($"{srcCode} {e.Operator} {tgtCode}");
        }

        for (var t = 0; t < ticks; t++)
        {
            var tickInputs = t == 0 ? inputs : [];
            lastResult = TickPipeline.Run(ctx, tickInputs);

            // Record per-tick signal values
            for (var i = 0; i < ctx.Signals.Count; i++)
                trajectories[ctx.Signals.Codes[i]].Add(ctx.Signals.Values[i]);

            // Capture cascade events with causal explanation
            foreach (var evt in lastResult.Events)
            {
                switch (evt)
                {
                    case KernelEvt.SignalChange sc:
                        // Find which edge caused this change
                        var cause = edgeLookup.TryGetValue(sc.Code, out var causes)
                            ? string.Join(", ", causes)
                            : (t == 0 ? "injection" : "decay");
                        cascadeSteps.Add(new CascadeEvent(
                            t + 1, sc.Code, Math.Round(sc.Old, 4), Math.Round(sc.New, 4), cause));
                        break;

                    case KernelEvt.GateFire gf:
                        gatesFired.Add(gates.FirstOrDefault(g => g.Id == gf.Id)?.Code ?? $"gate#{gf.Id}");
                        break;

                    case KernelEvt.GateBlock gb:
                        gatesBlocked.Add(gates.FirstOrDefault(g => g.Id == gb.Id)?.Code ?? $"gate#{gb.Id}");
                        break;

                    case KernelEvt.ConstraintViolated cv:
                        cascadeSteps.Add(new CascadeEvent(t + 1, "CONSTRAINT", 0, 0, $"violated: {cv.Expr}"));
                        break;

                    case KernelEvt.FailActive fa:
                        cascadeSteps.Add(new CascadeEvent(t + 1, "FAIL", 0, 0, $"{fa.Type}: {fa.Code}"));
                        break;
                }
            }

            if (lastResult.Stable) break;
        }

        // 8. Build deltas with trajectory + causal chain
        var deltas = new Dictionary<string, SignalDelta>();
        for (var i = 0; i < ctx.Signals.Count; i++)
        {
            var code = ctx.Signals.Codes[i];
            var diff = ctx.Signals.Values[i] - before[i];
            if (Math.Abs(diff) < 1e-6) continue;

            var pct = before[i] != 0 ? diff / Math.Abs(before[i]) * 100 : (diff > 0 ? 100 : -100);

            // Peak and trough during simulation
            var traj = trajectories[code];
            var peak = traj.Max();
            var trough = traj.Min();

            // Causal edges for this signal
            var causalEdges = edgeLookup.TryGetValue(code, out var ec) ? ec.ToArray() : [];

            deltas[code] = new SignalDelta(
                code,
                signalRows[i].region,
                Math.Round(before[i], 4),
                Math.Round(ctx.Signals.Values[i], 4),
                Math.Round(pct, 1),
                Math.Round(peak, 4),
                Math.Round(trough, 4),
                causalEdges);
        }

        // Collect formula outputs
        var formulaResults = new Dictionary<string, double>(ctx.FormulaOutputs);

        return new SimulationResult(
            deltas,
            (int)(lastResult?.TickNumber ?? 0),
            lastResult?.Stable ?? true,
            lastResult?.CascadeDepth ?? 0,
            [.. gatesFired],
            [.. gatesBlocked],
            cascadeSteps,
            formulaResults,
            unknownCodes.Count > 0 ? $"Unknown signals: {string.Join(", ", unknownCodes)}" : null);
    }

    /// <summary>
    /// Get a compact list of all signal codes with current state for the system prompt.
    /// </summary>
    public async Task<string> GetSignalSummaryAsync(Guid entityId, CancellationToken ct = default)
    {
        var pid = entityId.ToString();
        var rows = await db.Database
            .SqlQueryRaw<PgSignalSummaryRow>("""
                SELECT s.code, s.state, r.code AS region
                FROM v_signal_current s
                LEFT JOIN v_region_current r ON s.region_id = r.id AND s.entity_id = r.entity_id
                WHERE s.entity_id = {0}::uuid
                ORDER BY s.code
                """, pid)
            .ToListAsync(ct);

        if (rows.Count == 0) return "No signals.";

        var sb = new StringBuilder();
        foreach (var s in rows)
        {
            var region = s.region is not null ? $"@{s.region}" : "";
            sb.Append($"{s.code}[{s.state}]{region}, ");
        }
        if (sb.Length >= 2) sb.Length -= 2; // trim trailing ", "
        return sb.ToString();
    }

    /// <summary>
    /// Get graph metadata counts for the system prompt.
    /// </summary>
    public async Task<GraphMetadata> GetGraphMetadataAsync(Guid entityId, CancellationToken ct = default)
    {
        var pid = entityId.ToString();
        var counts = await db.Database
            .SqlQueryRaw<CountRow>("""
                SELECT 'edges' AS kind, count(*)::INT AS cnt FROM v_graph WHERE entity_id = {0}::uuid
                UNION ALL
                SELECT 'gates', count(*)::INT FROM v_gate_current WHERE entity_id = {0}::uuid
                UNION ALL
                SELECT 'dysreg', count(*)::INT FROM find_dysreg_cascades({0}::uuid)
                UNION ALL
                SELECT 'loops', count(*)::INT FROM find_feedback_loops({0}::uuid)
                """, pid)
            .ToListAsync(ct);

        return new GraphMetadata(
            counts.FirstOrDefault(c => c.kind == "edges")?.cnt ?? 0,
            counts.FirstOrDefault(c => c.kind == "gates")?.cnt ?? 0,
            counts.FirstOrDefault(c => c.kind == "dysreg")?.cnt ?? 0,
            counts.FirstOrDefault(c => c.kind == "loops")?.cnt ?? 0);
    }

    // ── Row types for raw SQL ───────────────────────────────────────────────
    // ReSharper disable InconsistentNaming NotAccessedPositionalProperty.Local
    private record PgSignalRow(
        int id, string code, string? region, string state,
        double value, double baseline, double confidence, string distribution,
        double tau_min_ms, double tau_max_ms, double range_low, double range_high);

    private record PgEdgeRow(
        string source_code, string target_code, string @operator, string operator_class,
        double gain, double noise_sigma, string transfer_fn,
        int delay_ms, double? clamp_lo, double? clamp_hi,
        int? gate_id, bool gate_active);

    private record PgGateRow(
        int id, string code, string type, double? threshold, string? expression,
        double? probability, bool latched, string? prompt, string? model,
        string? parse_map, string? fallback_expr, int? timeout_ms, int? cache_ms);

    private record PgSignalSummaryRow(string code, string state, string? region);
    private record CountRow(string kind, int cnt);
    // ReSharper restore InconsistentNaming NotAccessedPositionalProperty.Local
}

// ── Result types ────────────────────────────────────────────────────────────

public record SimulationResult(
    Dictionary<string, SignalDelta> Deltas,
    int TicksRun,
    bool Stable,
    int CascadeDepth,
    string[] GatesFired,
    string[] GatesBlocked,
    List<CascadeEvent> CascadeNarrative,
    Dictionary<string, double> FormulaResults,
    string? Warning = null)
{
    public static SimulationResult Empty(string warning) => new(
        [], 0, true, 0, [], [], [], [], warning);

    /// <summary>Format as rich text for LLM tool output — shows cascade story, not just final values.</summary>
    public string ToLlmText()
    {
        if (Warning is not null && Deltas.Count == 0)
            return Warning;

        var sb = new StringBuilder();
        if (Warning is not null)
            sb.AppendLine($"WARNING: {Warning}");

        sb.AppendLine($"Simulation: {TicksRun} ticks, cascade depth {CascadeDepth}, {(Stable ? "converged" : "still evolving")}");

        if (Deltas.Count == 0)
        {
            sb.AppendLine("No signal changes detected.");
            return sb.ToString();
        }

        // Final state: signals ordered by impact
        sb.AppendLine($"\n== Final State ({Deltas.Count} signals affected) ==");
        foreach (var (_, d) in Deltas.OrderByDescending(x => Math.Abs(x.Value.ChangePct)))
        {
            var region = d.Region is not null ? $"@{d.Region}" : "";
            var dir = d.ChangePct > 0 ? "+" : "";
            sb.Append($"  {d.Code}{region}: {d.Before} -> {d.Final} ({dir}{d.ChangePct:F1}%)");
            if (d.Peak != d.Final || d.Trough != d.Before)
                sb.Append($"  [peak={d.Peak}, trough={d.Trough}]");
            if (d.CausalEdges.Length > 0)
                sb.Append($"  caused by: {string.Join("; ", d.CausalEdges)}");
            sb.AppendLine();
        }

        // Cascade narrative: first 2 ticks in detail, summary after
        if (CascadeNarrative.Count > 0)
        {
            sb.AppendLine("\n== Cascade Propagation ==");
            var byTick = CascadeNarrative
                .Where(c => c.Code != "CONSTRAINT" && c.Code != "FAIL")
                .GroupBy(c => c.Tick)
                .OrderBy(g => g.Key);

            foreach (var tickGroup in byTick.Take(3))
            {
                sb.AppendLine($"  Tick {tickGroup.Key}:");
                foreach (var step in tickGroup.Take(10)) // cap per tick
                    sb.AppendLine($"    {step.Code}: {step.OldValue} -> {step.NewValue} ({step.Cause})");
                if (tickGroup.Count() > 10)
                    sb.AppendLine($"    ... +{tickGroup.Count() - 10} more changes");
            }

            var laterTicks = byTick.Skip(3).ToList();
            if (laterTicks.Count > 0)
            {
                var totalLater = laterTicks.Sum(g => g.Count());
                sb.AppendLine($"  Ticks {laterTicks.First().Key}-{laterTicks.Last().Key}: {totalLater} more signal changes (converging)");
            }

            // Constraints and failures
            var constraints = CascadeNarrative.Where(c => c.Code is "CONSTRAINT" or "FAIL").ToList();
            if (constraints.Count > 0)
            {
                sb.AppendLine("\n== Warnings ==");
                foreach (var c in constraints)
                    sb.AppendLine($"  [{c.Code}] tick {c.Tick}: {c.Cause}");
            }
        }

        if (GatesFired.Length > 0)
            sb.AppendLine($"\nGates fired: {string.Join(", ", GatesFired)}");
        if (GatesBlocked.Length > 0)
            sb.AppendLine($"Gates blocked: {string.Join(", ", GatesBlocked)}");

        // Formula outputs (LLM-defined computed metrics)
        if (FormulaResults.Count > 0)
        {
            sb.AppendLine("\n== Computed Metrics ==");
            foreach (var (name, value) in FormulaResults)
                sb.AppendLine($"  {name} = {value:F4}");
        }

        return sb.ToString();
    }
}

/// <summary>A single signal change in the cascade, with the cause (which edge or injection triggered it).</summary>
public record CascadeEvent(int Tick, string Code, double OldValue, double NewValue, string Cause);

/// <summary>
/// Rich delta for a single signal: final state, trajectory extremes, and causal edges.
/// Peak/trough show overshoot — a signal might spike to 0.95 then settle at 0.7.
/// </summary>
public record SignalDelta(
    string Code,
    string? Region,
    double Before,
    double Final,
    double ChangePct,
    double Peak,
    double Trough,
    string[] CausalEdges);

public record GraphMetadata(int EdgeCount, int GateCount, int DysregCascadeCount, int FeedbackLoopCount);
