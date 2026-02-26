namespace BioChain.AnalysisFramework;

/// <summary>
/// Computes signal co-occurrence statistics from observation data using
/// Pointwise Mutual Information (PMI). Pure computation — no DB, no DI.
/// </summary>
public static class CooccurrenceAnalyzer
{
    /// <summary>
    /// Analyze co-occurrence patterns from observations grouped by analysis run.
    /// Each analysis run is treated as a "document" — signals present together in the
    /// same run are considered co-occurring.
    /// </summary>
    /// <param name="observations">Tuples of (analysisRunId, signalKey) — one per observation</param>
    /// <param name="minSampleSize">Minimum co-occurrence count to include a pair</param>
    /// <returns>PMI-ranked signal associations</returns>
    public static List<SignalAssociation> Analyze(
        ReadOnlySpan<(Guid AnalysisRunId, string Signal)> observations,
        int minSampleSize = 3)
    {
        // Group observations by analysis run → set of signals per run
        var runSignals = new Dictionary<Guid, HashSet<string>>();
        foreach (var (runId, signal) in observations)
        {
            if (!runSignals.TryGetValue(runId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                runSignals[runId] = set;
            }
            set.Add(signal);
        }

        var totalRuns = (float)runSignals.Count;
        if (totalRuns < 2) return [];

        // Count individual signal frequency and pair co-occurrence
        var signalCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pairCount = new Dictionary<(string, string), int>();
        // Track conditional states: when A is present, what states does B have?
        var conditionalStates = new Dictionary<(string, string), Dictionary<string, int>>();

        foreach (var (_, signals) in runSignals)
        {
            var sorted = signals.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var s in sorted)
                signalCount[s] = signalCount.GetValueOrDefault(s) + 1;

            // All pairs (ordered to avoid duplicates)
            for (var i = 0; i < sorted.Count; i++)
            for (var j = i + 1; j < sorted.Count; j++)
            {
                var pair = (sorted[i], sorted[j]);
                pairCount[pair] = pairCount.GetValueOrDefault(pair) + 1;
            }
        }

        // Compute PMI for each pair
        var results = new List<SignalAssociation>();

        foreach (var (pair, count) in pairCount)
        {
            if (count < minSampleSize) continue;

            var pA = signalCount.GetValueOrDefault(pair.Item1) / totalRuns;
            var pB = signalCount.GetValueOrDefault(pair.Item2) / totalRuns;
            var pAB = count / totalRuns;

            if (pA <= 0 || pB <= 0) continue;

            var pmi = MathF.Log(pAB / (pA * pB));
            // Normalized PMI: range [-1, 1]
            var npmi = pAB > 0 ? pmi / -MathF.Log(pAB) : 0f;

            results.Add(new SignalAssociation(
                pair.Item1,
                pair.Item2,
                MathF.Round(pmi, 4),
                MathF.Round(npmi, 4),
                count,
                (int)totalRuns));
        }

        return results.OrderByDescending(r => MathF.Abs(r.Pmi)).ToList();
    }

    /// <summary>
    /// Detect temporal lag: does signal A consistently appear N analysis runs before signal B?
    /// </summary>
    /// <param name="observations">Tuples of (analysisRunId, signal, createdAt)</param>
    /// <param name="signalA">Source signal</param>
    /// <param name="signalB">Target signal</param>
    /// <returns>Mean lag in analysis runs (positive = A before B), or null if insufficient data</returns>
    public static float? DetectLag(
        ReadOnlySpan<(Guid AnalysisRunId, string Signal, DateTime CreatedAt)> observations,
        string signalA, string signalB)
    {
        var runsA = new List<DateTime>();
        var runsB = new List<DateTime>();

        foreach (var (_, signal, created) in observations)
        {
            if (signal.Equals(signalA, StringComparison.OrdinalIgnoreCase))
                runsA.Add(created);
            else if (signal.Equals(signalB, StringComparison.OrdinalIgnoreCase))
                runsB.Add(created);
        }

        if (runsA.Count < 2 || runsB.Count < 2) return null;

        // For each B occurrence, find nearest preceding A occurrence
        runsA.Sort();
        runsB.Sort();
        var lags = new List<float>();

        foreach (var bTime in runsB)
        {
            var nearest = runsA
                .Where(a => a < bTime)
                .OrderByDescending(a => a)
                .FirstOrDefault();

            if (nearest != default)
                lags.Add((float)(bTime - nearest).TotalDays);
        }

        return lags.Count >= 2 ? lags.Average() : null;
    }
}

public sealed record SignalAssociation(
    string SignalA,
    string SignalB,
    float Pmi,
    float NormalizedPmi,
    int CooccurrenceCount,
    int TotalRuns);
