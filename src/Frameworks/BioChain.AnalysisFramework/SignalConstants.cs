namespace BioChain.AnalysisFramework;

public static class SignalConstants
{
    public static readonly IReadOnlyDictionary<string, OptimalRange> PopulationRanges =
        new Dictionary<string, OptimalRange>(StringComparer.OrdinalIgnoreCase)
        {
            ["dopamine"] = new(0.55f, 0.40f, 0.70f),
            ["serotonin"] = new(0.55f, 0.40f, 0.70f),
            ["norepinephrine"] = new(0.50f, 0.35f, 0.65f),
            ["gaba"] = new(0.55f, 0.40f, 0.70f),
            ["acetylcholine"] = new(0.50f, 0.35f, 0.65f),
            ["endocannabinoid"] = new(0.50f, 0.35f, 0.65f),
            ["glutamate"] = new(0.50f, 0.35f, 0.65f),
            ["cortisol"] = new(0.45f, 0.30f, 0.60f),
            ["testosterone"] = new(0.50f, 0.35f, 0.65f),
            ["estradiol"] = new(0.50f, 0.35f, 0.65f),
            ["progesterone"] = new(0.50f, 0.35f, 0.65f),
            ["thyroid"] = new(0.50f, 0.35f, 0.65f),
            ["adrenaline"] = new(0.45f, 0.30f, 0.60f),
            ["melatonin"] = new(0.50f, 0.35f, 0.65f),
            ["dhea"] = new(0.50f, 0.35f, 0.65f),
            ["prolactin"] = new(0.50f, 0.35f, 0.65f),
            ["oxytocin_h"] = new(0.50f, 0.35f, 0.65f),
            ["oxytocin"] = new(0.50f, 0.35f, 0.65f),
            ["vasopressin"] = new(0.50f, 0.35f, 0.65f),
            ["endorphins"] = new(0.50f, 0.35f, 0.65f),
            ["enkephalins"] = new(0.50f, 0.35f, 0.65f),
            ["dynorphin"] = new(0.40f, 0.25f, 0.55f),
            ["substance_p"] = new(0.40f, 0.25f, 0.55f),
            ["crh"] = new(0.40f, 0.25f, 0.55f),
            ["npy"] = new(0.50f, 0.35f, 0.65f),
            ["bdnf"] = new(0.55f, 0.40f, 0.70f),
            ["orexin"] = new(0.50f, 0.35f, 0.65f),
        };
}
