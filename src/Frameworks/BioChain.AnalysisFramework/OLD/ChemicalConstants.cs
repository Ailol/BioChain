namespace BioChain.AnalysisFramework;

public enum DoseResponseCurve
{
    InvertedU,
    ModerateOptimal,
    CyclicOptimal,
    AcuteOnly,
    CircadianDependent,
    ContextDependent,
    Structural,
    MoreIsBetter,
    LowerIsBetter,
    MoreIsResilient,
    MoreIsPlastic,
    NarrowOptimal,
    Moderate,
}

public sealed record OptimalRange(float Low, float High, float Center, DoseResponseCurve Curve);

public static class ChemicalConstants
{
    // 26 population-level optimal ranges extracted from agent prompt dose-response data.
    // 10 chemicals have explicit numeric ranges in the prompts; 16 are derived from
    // curve semantics (e.g. more_is_resilient → 0.4-0.7, lower_is_better → 0.05-0.25).
    // These are species-level baselines, not individual targets.
    public static readonly IReadOnlyDictionary<string, OptimalRange> PopulationRanges =
        new Dictionary<string, OptimalRange>
        {
            // ── Neurotransmitter layer (7) ──
            // Explicit: inverted_u; optimal 0.3-0.6
            ["dopamine"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.InvertedU),
            // Explicit: moderate_optimal; optimal 0.3-0.6
            ["serotonin"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.ModerateOptimal),
            // Explicit: inverted_u; optimal 0.2-0.5
            ["norepinephrine"] = new(0.20f, 0.50f, 0.35f, DoseResponseCurve.InvertedU),
            // Explicit: moderate_optimal; optimal 0.3-0.6
            ["gaba"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.ModerateOptimal),
            // Explicit: moderate_optimal; optimal 0.3-0.5
            ["acetylcholine"] = new(0.30f, 0.50f, 0.40f, DoseResponseCurve.ModerateOptimal),
            // Explicit: moderate_optimal; optimal 0.2-0.5
            ["endocannabinoid"] = new(0.20f, 0.50f, 0.35f, DoseResponseCurve.ModerateOptimal),
            // Explicit: inverted_u; optimal 0.2-0.5
            ["glutamate"] = new(0.20f, 0.50f, 0.35f, DoseResponseCurve.InvertedU),

            // ── Hormone layer (10) ──
            // Explicit: inverted_u; optimal 0.2-0.4
            ["cortisol"] = new(0.20f, 0.40f, 0.30f, DoseResponseCurve.InvertedU),
            // Explicit: moderate_optimal; optimal 0.3-0.6
            ["testosterone"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.ModerateOptimal),
            // Cyclic: follicular rise=good, luteal decline=vulnerable; center around mid-cycle
            ["estradiol"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.CyclicOptimal),
            // Explicit: moderate_optimal; optimal 0.3-0.6
            ["progesterone"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.ModerateOptimal),
            // Narrow optimal: below=sluggish, above=agitation
            ["thyroid"] = new(0.35f, 0.55f, 0.45f, DoseResponseCurve.NarrowOptimal),
            // Acute only: brief adaptive spikes, chronic elevation is bad
            ["adrenaline"] = new(0.05f, 0.30f, 0.15f, DoseResponseCurve.AcuteOnly),
            // Circadian: strong nocturnal peak, clean daytime suppression
            ["melatonin"] = new(0.20f, 0.50f, 0.35f, DoseResponseCurve.CircadianDependent),
            // More is resilient: high DHEA:cortisol ratio = stress resilience
            ["dhea"] = new(0.40f, 0.70f, 0.55f, DoseResponseCurve.MoreIsResilient),
            // Context dependent: post-intimacy spike healthy; chronic high = bad
            ["prolactin"] = new(0.20f, 0.50f, 0.35f, DoseResponseCurve.ContextDependent),
            // ── Peptide layer (10) ──
            // Structural: high sustained = secure attachment architecture; with safety more=better
            ["oxytocin"] = new(0.40f, 0.70f, 0.55f, DoseResponseCurve.Structural),
            // Structural: high = strong partner preference
            ["vasopressin"] = new(0.35f, 0.65f, 0.50f, DoseResponseCurve.Structural),
            // More is better for bonding; deficit = social pain
            ["endorphins"] = new(0.40f, 0.70f, 0.55f, DoseResponseCurve.MoreIsBetter),
            // Moderate: baseline hedonic tone; deficit = restlessness
            ["enkephalins"] = new(0.30f, 0.60f, 0.45f, DoseResponseCurve.Moderate),
            // Lower is better: elevation = dysphoria, anhedonia
            ["dynorphin"] = new(0.05f, 0.25f, 0.15f, DoseResponseCurve.LowerIsBetter),
            // Lower is better: elevation = amplified emotional pain
            ["substance_p"] = new(0.05f, 0.25f, 0.15f, DoseResponseCurve.LowerIsBetter),
            // Lower is better: acute spike adaptive, chronic = anxiety disorders
            ["crh"] = new(0.05f, 0.25f, 0.15f, DoseResponseCurve.LowerIsBetter),
            // More is resilient: high = stress buffering
            ["npy"] = new(0.40f, 0.70f, 0.55f, DoseResponseCurve.MoreIsResilient),
            // More is plastic: high = open plasticity window
            ["bdnf"] = new(0.40f, 0.70f, 0.55f, DoseResponseCurve.MoreIsPlastic),
            // Context dependent: optimal arousal supports engagement
            ["orexin"] = new(0.25f, 0.55f, 0.40f, DoseResponseCurve.ContextDependent),
        };

    // Get effective optimal range for a person: population baseline + personal offset.
    // Offset starts at 0, shifts as more data accumulates about individual set points.
    public static OptimalRange GetEffectiveRange(string chemical, float personalOffset = 0f)
    {
        var pop = PopulationRanges[chemical];
        if (personalOffset == 0f)
            return pop;

        return pop with
        {
            Low = Math.Clamp(pop.Low + personalOffset, 0f, 1f),
            High = Math.Clamp(pop.High + personalOffset, 0f, 1f),
            Center = Math.Clamp(pop.Center + personalOffset, 0f, 1f),
        };
    }
}
