using Neo4j.Driver;
using Microsoft.Extensions.Logging;
using BioChain.Kernel.Signals;

namespace BioChain.Kernel.Graph;

/// <summary>
/// Direct Neo4j sync from in-memory tick engine state (SignalRow/EdgeRow/GateRow).
/// Used by the Orleans WorldGrain to push graph changes after simulation ticks.
/// Complements <see cref="Neo4jGraphStore"/> which syncs from PG JSON exports.
/// </summary>
public sealed class Neo4jTickSync : IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jTickSync> _log;

    public Neo4jTickSync(IDriver driver, ILogger<Neo4jTickSync> log) { _driver = driver; _log = log; }

    /// <summary>Full graph rebuild for a subject. Enriched with numeric properties.</summary>
    public async Task RebuildGraphAsync(Guid subjectId, SignalRow[] signals, EdgeRow[] edges, GateRow[] gates)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // Clear existing
            await tx.RunAsync("MATCH (n {subject_id: $sid}) DETACH DELETE n",
                new { sid = subjectId.ToString() });

            // Create signal nodes with numeric properties
            if (signals.Length > 0)
            {
                await tx.RunAsync(@"
                    UNWIND $rows AS r
                    CREATE (s:Signal {
                        subject_id: r.sid, code: r.code, region: r.region,
                        state: r.state, value: r.value, baseline: r.baseline,
                        confidence: r.conf, tau_min_ms: r.tau, range_low: r.lo, range_high: r.hi
                    })",
                    new { rows = signals.Select(s => new {
                        sid = subjectId.ToString(), code = s.Code, region = s.Region ?? "",
                        state = s.State, value = s.Value, baseline = s.Baseline,
                        conf = s.Confidence, tau = s.TauMinMs, lo = s.RangeLow, hi = s.RangeHigh
                    }).ToArray() });
            }

            // Create edges with numeric properties
            if (edges.Length > 0)
            {
                foreach (var e in edges)
                {
                    var srcCode = signals.FirstOrDefault(s => s.Id == e.SourceId)?.Code ?? "";
                    var tgtCode = signals.FirstOrDefault(s => s.Id == e.TargetId)?.Code ?? "";
                    if (srcCode == "" || tgtCode == "") continue;

                    await tx.RunAsync($@"
                        MATCH (s:Signal {{subject_id: $sid, code: $src}})
                        MATCH (t:Signal {{subject_id: $sid, code: $tgt}})
                        CREATE (s)-[:{e.OperatorClass.ToUpperInvariant()} {{
                            operator: $op, gain: $gain, noise_sigma: $noise,
                            transfer_fn: $tfn, delay_ms: $delay, clamp_lo: $lo, clamp_hi: $hi
                        }}]->(t)",
                        new {
                            sid = subjectId.ToString(), src = srcCode, tgt = tgtCode,
                            op = e.Operator, gain = e.Gain, noise = e.NoiseSigma,
                            tfn = e.TransferFn, delay = e.DelayMs,
                            lo = e.ClampLo ?? 0.0, hi = e.ClampHi ?? 0.0
                        });
                }
            }
        });

        _log.LogDebug("[Neo4jTickSync] Rebuilt graph for {SubjectId}: {Signals} signals, {Edges} edges",
            subjectId, signals.Length, edges.Length);
    }

    /// <summary>Lightweight sync: update signal values only (between full rebuilds).</summary>
    public async Task SyncSignalValuesAsync(Guid subjectId, SignalColumns signals)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            for (int i = 0; i < signals.Count; i++)
            {
                await tx.RunAsync(@"
                    MATCH (s:Signal {subject_id: $sid, code: $code})
                    SET s.value = $val, s.confidence = $conf",
                    new { sid = subjectId.ToString(), code = signals.Codes[i],
                          val = signals.Values[i], conf = signals.Confidences[i] });
            }
        });
    }

    public async ValueTask DisposeAsync() => _driver.Dispose();
}
