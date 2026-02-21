namespace NeuroGateway.AnalysisFramework;

public sealed record Prescription(
    string Modality,
    string Rationale,
    List<string> TargetChemicals,
    float Priority
);

public sealed record OvertrainingAlert(string Indicator, string Recommendation);

public static class DeficitPrescriber
{
    private const float LowThresholdOffset = -0.05f; // below range.Low - offset → deficit
    private const float HighThresholdOffset = 0.05f; // above range.High + offset → excess
    private const float MinObservationsForPrescription = 2;
    private const float BurnoutCortisolDheaRatio = 2.0f;
    private const float GrowthBdnfThreshold = 0.5f;
    private const float GrowthCortisolCeiling = 0.35f;

    // Chemical → (condition, modality, rationale) mappings from neuroscience
    private static readonly List<PrescriptionRule> Rules =
    [
        new("gaba", RuleType.Low, "Yoga", "Low GABA → parasympathetic activation through slow movement and breathwork"),
        new("gaba", RuleType.Low, "Meditation", "Low GABA → GABA-A receptor upregulation via mindfulness practice"),
        new("gaba", RuleType.Low, "Breathwork", "Low GABA → vagal tone stimulation through controlled breathing"),
        new("cortisol", RuleType.High, "Nature Walks", "High cortisol → HPA axis downregulation through green space exposure"),
        new("cortisol", RuleType.High, "Swimming", "High cortisol → cortisol clearance through moderate steady-state aquatic exercise"),
        new("cortisol", RuleType.High, "Gentle Cycling", "High cortisol → parasympathetic rebound without sympathetic overdrive"),
        new("bdnf", RuleType.Low, "Running", "Low BDNF → aerobic exercise triggers hippocampal BDNF synthesis via FNDC5/irisin"),
        new("bdnf", RuleType.Low, "HIIT", "Low BDNF → high-intensity intervals produce acute BDNF spike via lactate signaling"),
        new("endorphins", RuleType.Low, "Social Sports", "Low endorphins → synchronized group movement triggers collective endorphin release"),
        new("endorphins", RuleType.Low, "Dance", "Low endorphins → rhythmic social movement activates mu-opioid reward circuits"),
        new("endorphins", RuleType.Low, "Running", "Low endorphins → sustained aerobic effort triggers beta-endorphin release"),
        new("serotonin", RuleType.Low, "Rhythmic Running", "Low serotonin → repetitive rhythmic movement upregulates tryptophan hydroxylase"),
        new("serotonin", RuleType.Low, "Outdoor Activity", "Low serotonin → sunlight exposure increases serotonin synthesis via retinal pathways"),
        new("serotonin", RuleType.Low, "Swimming", "Low serotonin → rhythmic aquatic exercise with light exposure"),
        new("dopamine", RuleType.Low, "Novel Exercise", "Low dopamine → novelty-seeking exercise activates VTA reward prediction circuits"),
        new("dopamine", RuleType.Low, "Competitive Sports", "Low dopamine → competitive challenge triggers mesolimbic dopamine via reward anticipation"),
        new("dopamine", RuleType.Low, "HIIT", "Low dopamine → high-intensity intervals produce acute dopamine release in striatum"),
        new("norepinephrine", RuleType.High, "Yoga", "High norepinephrine → parasympathetic activation to dampen LC hyperarousal"),
        new("norepinephrine", RuleType.High, "Tai Chi", "High norepinephrine → slow deliberate movement reduces sympathetic overdrive"),
        new("npy", RuleType.Low, "Endurance Training", "Low NPY → sustained aerobic exercise upregulates NPY synthesis in arcuate nucleus"),
        new("endocannabinoid", RuleType.Low, "Moderate Steady-State Cardio", "Low endocannabinoid → 30+ min moderate cardio triggers anandamide release (runner's high)"),
        new("dhea", RuleType.Low, "Resistance Training", "Low DHEA → progressive resistance exercise stimulates adrenal DHEA output"),
    ];

    public static List<Prescription> Prescribe(
        IReadOnlyDictionary<string, float> levels,
        IReadOnlyDictionary<string, int> obsCounts
    )
    {
        var prescriptions = new List<Prescription>();
        var modalitySeen = new HashSet<string>();

        foreach (var rule in Rules)
        {
            if (!levels.TryGetValue(rule.Chemical, out var level))
                continue;

            obsCounts.TryGetValue(rule.Chemical, out var count);
            if (count < MinObservationsForPrescription)
                continue;

            if (!ChemicalConstants.PopulationRanges.TryGetValue(rule.Chemical, out var range))
                continue;

            var isDeficit = rule.Type == RuleType.Low && level < range.Low + LowThresholdOffset;
            var isExcess = rule.Type == RuleType.High && level > range.High + HighThresholdOffset;

            if (!isDeficit && !isExcess)
                continue;

            // Priority: how far outside optimal
            var deviation = rule.Type == RuleType.Low
                ? (range.Low - level) / range.Low
                : (level - range.High) / (1f - range.High + 0.01f);
            var priority = Math.Clamp(deviation, 0f, 1f);

            // Deduplicate modalities: first rule wins, add chemical to targets
            if (modalitySeen.Contains(rule.Modality))
            {
                var existing = prescriptions.First(p => p.Modality == rule.Modality);
                if (!existing.TargetChemicals.Contains(rule.Chemical))
                    existing.TargetChemicals.Add(rule.Chemical);
                continue;
            }

            modalitySeen.Add(rule.Modality);
            prescriptions.Add(
                new Prescription(
                    rule.Modality,
                    rule.Rationale,
                    [rule.Chemical],
                    priority
                )
            );
        }

        return prescriptions.OrderByDescending(p => p.Priority).ToList();
    }

    // Detect overtraining from chemical markers:
    // High cortisol + low testosterone + low BDNF → overtraining syndrome
    public static OvertrainingAlert? DetectOvertraining(
        IReadOnlyDictionary<string, float> levels
    )
    {
        levels.TryGetValue("cortisol", out var cortisol);
        levels.TryGetValue("testosterone", out var testosterone);
        levels.TryGetValue("bdnf", out var bdnf);

        if (cortisol <= 0.5f || testosterone >= 0.3f || bdnf >= 0.3f)
            return null;

        return new OvertrainingAlert(
            $"cortisol={cortisol:F2}, testosterone={testosterone:F2}, BDNF={bdnf:F2}",
            "Reduce training intensity. Prioritize sleep and recovery. Consider deload week."
        );
    }

    // Burnout: cortisol:DHEA ratio > 2.0 signals HPA output without neuroprotective buffering
    public static (bool AtRisk, float Ratio, string? Note) DetectBurnout(
        IReadOnlyDictionary<string, float> levels
    )
    {
        levels.TryGetValue("cortisol", out var cortisol);
        levels.TryGetValue("dhea", out var dhea);

        if (dhea < 0.01f)
            return (cortisol > 0.3f, float.PositiveInfinity, "DHEA depleted with active cortisol — severe burnout risk");

        var ratio = cortisol / dhea;
        if (ratio > BurnoutCortisolDheaRatio)
            return (true, ratio, $"Cortisol:DHEA ratio {ratio:F1} exceeds threshold — stress output exceeds resilience buffer");

        return (false, ratio, null);
    }

    // Growth window: high BDNF + low cortisol = open plasticity
    public static (bool Open, string? Note) DetectGrowthWindow(
        IReadOnlyDictionary<string, float> levels
    )
    {
        levels.TryGetValue("bdnf", out var bdnf);
        levels.TryGetValue("cortisol", out var cortisol);

        if (bdnf > GrowthBdnfThreshold && cortisol < GrowthCortisolCeiling)
            return (true, "BDNF elevated with low cortisol — plasticity window is open for learning and pattern change");

        return (false, null);
    }

    private enum RuleType
    {
        Low,
        High,
    }

    private sealed record PrescriptionRule(
        string Chemical,
        RuleType Type,
        string Modality,
        string Rationale
    );
}
