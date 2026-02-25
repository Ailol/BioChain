namespace BioChain.AnalysisFramework.OLD;

public enum ChemicalTrend
{
    Stable,
    Rising,
    Declining,
    Oscillating,
    AtRisk,
}

public sealed record ChemicalForecast(
    string Chemical,
    ChemicalTrend Trend,
    float CurrentLevel,
    float ProjectedLevel,
    float Velocity,
    bool ApproachingOptimal,
    bool DriftingFromOptimal,
    string? RiskNote
);

public sealed record CascadeAlert(
    string TriggerChemical,
    List<string> AffectedChemicals,
    string Mechanism,
    string Severity
);

public sealed record PersonalForecast(
    List<ChemicalForecast> Chemicals,
    List<CascadeAlert> ActiveCascades,
    List<string> StableFoundation,
    List<string> InFlux,
    string OverallTrajectory,
    string Narrative
);

public static class TrajectoryForecaster
{
    private const float StableVelocityThreshold = 0.001f;
    private const float StableVarianceThreshold = 0.001f;
    private const float TrendVelocityThreshold = 0.005f;
    private const float OscillationVarianceThreshold = 0.003f;
    private const float RiskHighThreshold = 0.85f;
    private const float RiskLowThreshold = 0.10f;
    private const int TailWindow = 10;
    private const int MinCascadeTargets = 2;

    public static PersonalForecast Analyze(
        InternalSimulationResult simulation,
        IReadOnlyDictionary<
            (string Source, string Target),
            (float ModFactor, string? Mechanism)
        >? interactions = null
    )
    {
        var chemicals = simulation.Initial.Keys.ToList();
        var forecasts = new List<ChemicalForecast>();
        var stableFoundation = new List<string>();
        var inFlux = new List<string>();

        foreach (var chemical in chemicals)
        {
            var values = simulation
                .Trajectory.Select(s =>
                    s.Levels.TryGetValue(chemical, out var v) ? v : 0f
                )
                .ToList();

            var current = simulation.Initial.TryGetValue(chemical, out var c) ? c : 0f;
            var tailStart = Math.Max(0, values.Count - TailWindow);
            var tail = values.Skip(tailStart).ToList();

            if (tail.Count == 0)
            {
                forecasts.Add(
                    new ChemicalForecast(
                        chemical,
                        ChemicalTrend.Stable,
                        current,
                        current,
                        0f,
                        false,
                        false,
                        null
                    )
                );
                stableFoundation.Add(chemical);
                continue;
            }

            var projected = tail[^1];
            var velocity = tail.Count > 1 ? (tail[^1] - tail[0]) / tail.Count : 0f;
            var tailMean = tail.Average();
            var tailVariance = tail.Count > 1
                ? tail.Select(v => (v - tailMean) * (v - tailMean)).Sum() / (tail.Count - 1)
                : 0f;

            var trend = ClassifyTrend(velocity, tailVariance, projected);

            var optimalCenter = ChemicalConstants.PopulationRanges.TryGetValue(
                chemical,
                out var range
            )
                ? range.Center
                : 0.5f;

            var currentDist = MathF.Abs(current - optimalCenter);
            var projectedDist = MathF.Abs(projected - optimalCenter);
            var approachingOptimal = projectedDist < currentDist - 0.01f;
            var driftingFromOptimal = projectedDist > currentDist + 0.01f;

            string? riskNote = null;
            if (trend == ChemicalTrend.AtRisk)
                riskNote = projected > RiskHighThreshold
                    ? $"{chemical} projected to extreme high ({projected:F2})"
                    : $"{chemical} projected to extreme low ({projected:F2})";

            forecasts.Add(
                new ChemicalForecast(
                    chemical,
                    trend,
                    current,
                    projected,
                    velocity,
                    approachingOptimal,
                    driftingFromOptimal,
                    riskNote
                )
            );

            if (trend == ChemicalTrend.Stable)
                stableFoundation.Add(chemical);
            else
                inFlux.Add(chemical);
        }

        var cascades = DetectCascades(forecasts, interactions);
        var overallTrajectory = ClassifyOverall(forecasts, cascades);
        var narrative = BuildNarrative(forecasts, cascades, overallTrajectory);

        return new PersonalForecast(
            forecasts,
            cascades,
            stableFoundation,
            inFlux,
            overallTrajectory,
            narrative
        );
    }

    private static ChemicalTrend ClassifyTrend(
        float velocity,
        float variance,
        float projected
    )
    {
        if (projected > RiskHighThreshold || projected < RiskLowThreshold)
            return ChemicalTrend.AtRisk;

        if (variance < StableVarianceThreshold && MathF.Abs(velocity) < StableVelocityThreshold)
            return ChemicalTrend.Stable;

        if (velocity > TrendVelocityThreshold)
            return ChemicalTrend.Rising;

        if (velocity < -TrendVelocityThreshold)
            return ChemicalTrend.Declining;

        if (variance > OscillationVarianceThreshold)
            return ChemicalTrend.Oscillating;

        return ChemicalTrend.Stable;
    }

    private static List<CascadeAlert> DetectCascades(
        List<ChemicalForecast> forecasts,
        IReadOnlyDictionary<
            (string Source, string Target),
            (float ModFactor, string? Mechanism)
        >? interactions
    )
    {
        if (interactions is null)
            return [];

        var cascades = new List<CascadeAlert>();
        var forecastMap = forecasts.ToDictionary(f => f.Chemical);

        // Find chemicals that are rising or at risk
        var triggers = forecasts
            .Where(f => f.Trend is ChemicalTrend.Rising or ChemicalTrend.AtRisk)
            .ToList();

        foreach (var trigger in triggers)
        {
            // Find this chemical's negative targets that are declining
            var affectedTargets = interactions
                .Where(kvp =>
                    kvp.Key.Source == trigger.Chemical
                    && kvp.Value.ModFactor < 0
                    && forecastMap.TryGetValue(kvp.Key.Target, out var targetForecast)
                    && targetForecast.Trend is ChemicalTrend.Declining or ChemicalTrend.AtRisk
                )
                .Select(kvp => (Target: kvp.Key.Target, kvp.Value.Mechanism))
                .ToList();

            if (affectedTargets.Count < MinCascadeTargets)
                continue;

            var targetNames = affectedTargets.Select(t => t.Target).ToList();
            var mechanisms = affectedTargets
                .Where(t => t.Mechanism is not null)
                .Select(t => t.Mechanism!)
                .ToList();

            var mechanismText =
                $"{trigger.Chemical} → {string.Join(", ", targetNames.Select(t => $"{t} suppression"))}";
            if (mechanisms.Count > 0)
                mechanismText += $" via {string.Join(", ", mechanisms)}";

            var severity =
                targetNames.Count >= 4 ? "alert"
                : targetNames.Count >= 3 ? "warning"
                : "watch";

            cascades.Add(
                new CascadeAlert(trigger.Chemical, targetNames, mechanismText, severity)
            );
        }

        return cascades;
    }

    private static string ClassifyOverall(
        List<ChemicalForecast> forecasts,
        List<CascadeAlert> cascades
    )
    {
        var stableCount = forecasts.Count(f => f.Trend == ChemicalTrend.Stable);
        var approachingCount = forecasts.Count(f => f.ApproachingOptimal);
        var atRiskCount = forecasts.Count(f => f.Trend == ChemicalTrend.AtRisk);

        var cortisolRising = forecasts.Any(f =>
            f.Chemical == "cortisol"
            && f.Trend is ChemicalTrend.Rising or ChemicalTrend.AtRisk
        );
        var crhRising = forecasts.Any(f =>
            f.Chemical == "crh"
            && f.Trend is ChemicalTrend.Rising or ChemicalTrend.AtRisk
        );
        var bdnfHigh = forecasts.Any(f => f.Chemical == "bdnf" && f.ProjectedLevel > 0.5f);
        var cortisolLow = forecasts.Any(f => f.Chemical == "cortisol" && f.ProjectedLevel < 0.3f);
        var stressChemicalsDeclining = forecasts.Count(f =>
            f.Chemical is "cortisol" or "crh" or "dynorphin" or "substance_p"
            && f.Trend == ChemicalTrend.Declining
        );

        if ((cortisolRising || crhRising) && cascades.Count > 0)
            return "Under Pressure";
        if (bdnfHigh && cortisolLow)
            return "Growth Window";
        if (stressChemicalsDeclining >= 2)
            return "Recovery Phase";
        if (stableCount > forecasts.Count / 2 && approachingCount > 0)
            return "Stabilizing";
        if (atRiskCount >= 3)
            return "Under Pressure";

        return "Stabilizing";
    }

    private static string BuildNarrative(
        List<ChemicalForecast> forecasts,
        List<CascadeAlert> cascades,
        string overallTrajectory
    )
    {
        var parts = new List<string>();

        parts.Add(
            overallTrajectory switch
            {
                "Under Pressure"
                    => "Your chemistry is showing signs of stress activation.",
                "Growth Window"
                    => "You're in a favorable state for learning and growth.",
                "Recovery Phase"
                    => "Your stress markers are declining — recovery is underway.",
                "Stabilizing"
                    => "Your chemical profile is settling into a stable pattern.",
                _ => "Your chemistry is in transition.",
            }
        );

        var rising = forecasts.Where(f => f.Trend == ChemicalTrend.Rising).ToList();
        var declining = forecasts.Where(f => f.Trend == ChemicalTrend.Declining).ToList();

        if (rising.Count > 0)
            parts.Add(
                $"{string.Join(", ", rising.Select(f => f.Chemical))} "
                    + (rising.Count == 1 ? "is" : "are")
                    + " trending upward."
            );

        if (declining.Count > 0)
            parts.Add(
                $"{string.Join(", ", declining.Select(f => f.Chemical))} "
                    + (declining.Count == 1 ? "is" : "are")
                    + " trending downward."
            );

        if (cascades.Count > 0)
        {
            var alertCascade = cascades.FirstOrDefault(c => c.Severity == "alert");
            if (alertCascade is not null)
                parts.Add(
                    $"Active cascade detected: {alertCascade.TriggerChemical} is suppressing {string.Join(" and ", alertCascade.AffectedChemicals)}."
                );
        }

        return string.Join(" ", parts);
    }
}
