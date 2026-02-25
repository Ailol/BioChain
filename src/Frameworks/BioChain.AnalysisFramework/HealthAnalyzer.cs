namespace BioChain.AnalysisFramework;

/// <summary>
/// Health indicator detection from signal level maps.
/// Pure functions — no DB access, no side effects.
/// </summary>
public static class HealthAnalyzer
{
    public static (bool Risk, float Ratio, string? Note) DetectBurnout(
        IReadOnlyDictionary<string, float> levels)
    {
        levels.TryGetValue("cortisol", out var cortisol);
        levels.TryGetValue("dhea", out var dhea);
        if (dhea > 0)
        {
            var ratio = cortisol / dhea;
            if (ratio > 2f)
                return (true, ratio, "Elevated cortisol-to-DHEA ratio suggests burnout risk.");
        }
        return (false, 0f, null);
    }

    public static (bool Open, string? Note) DetectGrowthWindow(
        IReadOnlyDictionary<string, float> levels)
    {
        levels.TryGetValue("bdnf", out var bdnf);
        levels.TryGetValue("dopamine", out var da);
        if (bdnf > 0.5f && da > 0.5f)
            return (true, "BDNF and dopamine levels support neuroplasticity.");
        return (false, null);
    }

    public static OvertrainingResult? DetectOvertraining(
        IReadOnlyDictionary<string, float> levels)
    {
        levels.TryGetValue("cortisol", out var cortisol);
        levels.TryGetValue("testosterone", out var test);
        if (cortisol > 0.7f && test < 0.3f)
            return new OvertrainingResult("High cortisol with low testosterone",
                "Consider reducing training intensity.");
        return null;
    }

    public static List<Prescription> Prescribe(
        IReadOnlyDictionary<string, float> levels,
        IReadOnlyDictionary<string, int> counts)
    {
        var prescriptions = new List<Prescription>();

        // Identify deficits: signals below population center with enough observations
        foreach (var (signal, level) in levels)
        {
            if (!SignalConstants.PopulationRanges.TryGetValue(signal, out var range))
                continue;
            if (!counts.TryGetValue(signal, out var count) || count < 2)
                continue;
            if (level >= range.Low)
                continue;

            var deficit = range.Center - level;
            var priority = deficit * count;

            var (modality, rationale) = signal.ToLowerInvariant() switch
            {
                "dopamine" => ("Novel learning + incremental challenges",
                    "Dopamine production is driven by novelty and achievable goal completion via D1/D2 receptor activation."),
                "serotonin" => ("Morning bright-light exposure + social rhythm",
                    "Serotonin synthesis depends on tryptophan hydroxylase activity, enhanced by light and consistent social schedules."),
                "gaba" => ("Diaphragmatic breathing + progressive muscle relaxation",
                    "Slow breathing activates vagal afferents that enhance GABAergic tone in the amygdala."),
                "bdnf" => ("Vigorous aerobic exercise (above lactate threshold)",
                    "BDNF release from hippocampus is intensity-dependent, requiring vigorous effort to cross the release threshold."),
                "melatonin" => ("Light hygiene: eliminate blue light 90min before bed",
                    "Melatonin synthesis by the pineal gland is suppressed by retinal light input via the retinohypothalamic tract."),
                "cortisol" when level > range.High => ("Stress reduction + sleep schedule consistency",
                    "Cortisol awakening response depends on circadian consistency; irregular schedules dysregulate the HPA axis."),
                "npy" => ("Regular moderate exercise + adequate caloric intake",
                    "NPY production requires both physical activity signals and metabolic sufficiency for sustained release."),
                _ => ($"Targeted support for {signal}",
                    $"Signal {signal} is below optimal range ({level:F2} < {range.Low:F2}). Consider lifestyle modifications.")
            };

            prescriptions.Add(new Prescription(modality, rationale, [signal], priority));
        }

        return prescriptions.OrderByDescending(p => p.Priority).ToList();
    }
}
