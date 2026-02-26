namespace BioChain.AnalysisFramework;

/// <summary>
/// Signal dynamics propagation and trajectory forecasting.
/// Pure functions — no DB access.
/// </summary>
public static class ForecastEngine
{
    private const int MaxHops = 3;
    private const float HopDecay = 0.5f;

    /// <summary>
    /// Propagate signal levels through the interaction graph with multi-hop support.
    /// Applies modulation factors from known interactions, optionally blended with
    /// empirical co-occurrence PMI from observation data.
    /// </summary>
    /// <param name="levels">Current signal levels (0-1)</param>
    /// <param name="interactions">Theoretical interactions with mod factors</param>
    /// <param name="counts">Observation count per signal</param>
    /// <param name="variances">Level variance per signal</param>
    /// <param name="empiricalPmi">Optional: PMI from CooccurrenceAnalyzer (signal pair → PMI)</param>
    public static Dictionary<string, float> PropagateSignals(
        IReadOnlyDictionary<string, float> levels,
        IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)> interactions,
        IReadOnlyDictionary<string, int> counts,
        IReadOnlyDictionary<string, float> variances,
        IReadOnlyDictionary<(string, string), float>? empiricalPmi = null)
    {
        var result = new Dictionary<string, float>(levels, StringComparer.OrdinalIgnoreCase);
        var deltas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        for (var hop = 0; hop < MaxHops; hop++)
        {
            deltas.Clear();
            var hopDecay = MathF.Pow(HopDecay, hop);

            foreach (var ((source, target), (modFactor, _)) in interactions)
            {
                if (!result.TryGetValue(source, out var sourceLevel)) continue;
                if (!result.ContainsKey(target)) continue;
                if (!counts.TryGetValue(source, out var sourceCount) || sourceCount < 2) continue;

                var sourceRange = SignalConstants.PopulationRanges.GetValueOrDefault(source);
                var center = sourceRange?.Center ?? 0.5f;
                var deviation = sourceLevel - center;

                // Blend theoretical mod factor with empirical PMI
                var theoreticalWeight = modFactor;
                var empiricalWeight = 0f;
                if (empiricalPmi is not null)
                {
                    // Try both orderings of the pair
                    if (!empiricalPmi.TryGetValue((source, target), out empiricalWeight))
                        empiricalPmi.TryGetValue((target, source), out empiricalWeight);
                }

                // Sigmoid activation on empirical PMI → [0, 1]
                var empiricalActivation = 1f / (1f + MathF.Exp(-empiricalWeight));
                var combinedWeight = theoreticalWeight * 0.6f + empiricalActivation * 0.4f;

                var targetVar = variances.GetValueOrDefault(target, 0f);
                var dampening = 1f / (1f + targetVar * 10f);
                var delta = deviation * combinedWeight * 0.1f * dampening * hopDecay;

                deltas[target] = deltas.GetValueOrDefault(target) + delta;
            }

            // Apply accumulated deltas
            foreach (var (signal, delta) in deltas)
                result[signal] = Math.Clamp(result[signal] + delta, 0f, 1f);
        }

        return result;
    }

    /// <summary>
    /// Generate a personal forecast from current signal levels and their interactions.
    /// </summary>
    public static PersonalForecast Forecast(
        IReadOnlyDictionary<string, float> signalLevels,
        IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)> interactions)
    {
        var signals = new List<SignalForecast>();
        var cascades = new List<CascadeAlert>();
        var stable = new List<string>();
        var inFlux = new List<string>();

        foreach (var (signal, level) in signalLevels)
        {
            var range = SignalConstants.PopulationRanges.GetValueOrDefault(signal);
            var center = range?.Center ?? 0.5f;
            var low = range?.Low ?? 0.35f;
            var high = range?.High ?? 0.65f;

            // Compute projected level based on interaction-driven drift
            var projectedDelta = 0f;
            var affectedBy = new List<string>();

            foreach (var ((source, target), (modFactor, mechanism)) in interactions)
            {
                if (!target.Equals(signal, StringComparison.OrdinalIgnoreCase)) continue;
                if (!signalLevels.TryGetValue(source, out var sourceLevel)) continue;

                var sourceDeviation = sourceLevel - (SignalConstants.PopulationRanges.GetValueOrDefault(source)?.Center ?? 0.5f);
                projectedDelta += sourceDeviation * modFactor * 0.05f;
                if (MathF.Abs(sourceDeviation) > 0.15f)
                    affectedBy.Add(source);
            }

            var projectedLevel = Math.Clamp(level + projectedDelta, 0f, 1f);
            var velocity = projectedDelta;
            var trend = velocity > 0.02f ? ForecastTrend.Rising
                : velocity < -0.02f ? ForecastTrend.Falling
                : ForecastTrend.Stable;

            var approachingOptimal = (level < center && projectedLevel > level) ||
                                     (level > center && projectedLevel < level);
            var driftingFromOptimal = (level >= low && level <= high) &&
                                      (projectedLevel < low || projectedLevel > high);

            string? riskNote = null;
            if (projectedLevel < low)
                riskNote = $"{signal} projected below optimal range ({projectedLevel:F2} < {low:F2}).";
            else if (projectedLevel > high)
                riskNote = $"{signal} projected above optimal range ({projectedLevel:F2} > {high:F2}).";

            signals.Add(new SignalForecast(signal, trend, level, projectedLevel,
                velocity, approachingOptimal, driftingFromOptimal, riskNote));

            if (trend == ForecastTrend.Stable)
                stable.Add(signal);
            else
                inFlux.Add(signal);

            // Cascade detection: if this signal is strongly deviated and affects others
            if (MathF.Abs(level - center) > 0.2f && affectedBy.Count == 0)
            {
                var affectedSignals = interactions
                    .Where(i => i.Key.Source.Equals(signal, StringComparison.OrdinalIgnoreCase))
                    .Select(i => i.Key.Target)
                    .Distinct()
                    .ToList();

                if (affectedSignals.Count > 1)
                {
                    var severity = MathF.Abs(level - center) > 0.35f ? "High" : "Medium";
                    cascades.Add(new CascadeAlert(signal, affectedSignals,
                        level > center ? "Excess propagation" : "Deficit cascade", severity));
                }
            }
        }

        var trajectory = inFlux.Count > stable.Count ? "In Flux"
            : cascades.Count > 0 ? "Cascading"
            : "Stable";

        var narrative = BuildNarrative(signals, cascades, stable, inFlux);

        return new PersonalForecast(signals, cascades, stable, inFlux, trajectory, narrative);
    }

    private static string BuildNarrative(
        List<SignalForecast> signals, List<CascadeAlert> cascades,
        List<string> stable, List<string> inFlux)
    {
        if (signals.Count == 0)
            return "Insufficient data for trajectory analysis.";

        var parts = new List<string>();

        if (stable.Count > 0)
            parts.Add($"{stable.Count} signal(s) are stable");

        var rising = signals.Where(s => s.Trend == ForecastTrend.Rising).Select(s => s.Signal).ToList();
        var falling = signals.Where(s => s.Trend == ForecastTrend.Falling).Select(s => s.Signal).ToList();

        if (rising.Count > 0)
            parts.Add($"{string.Join(", ", rising.Take(3))} trending upward");
        if (falling.Count > 0)
            parts.Add($"{string.Join(", ", falling.Take(3))} trending downward");
        if (cascades.Count > 0)
            parts.Add($"{cascades.Count} active cascade(s) detected");

        return string.Join(". ", parts) + ".";
    }
}
