namespace NeuroGateway.AnalysisFramework;

public static class DimensionDefinitions
{
    public sealed record DimensionDef(string Name, string Category, string Description);

    public static readonly IReadOnlyList<DimensionDef> All =
    [
        // ── Drive & Trajectory ──────────────────────────────────────────────
        new("Ambition",
            "Drive & Trajectory",
            "Goal-directed drive, escalating scope of influence, reward-seeking behavior, competitive positioning, proactive advancement toward higher-status outcomes and expanded control over resources."),

        new("Risk Tolerance",
            "Drive & Trajectory",
            "Willingness to act under uncertainty, comfort with novel or unpredictable situations, exploratory behavior, reduced threat sensitivity, tolerance for potential loss in pursuit of gain."),

        new("Persistence",
            "Drive & Trajectory",
            "Sustained effort despite obstacles, resistance to discouragement, delayed gratification tolerance, continued engagement with difficult tasks, recovery from setbacks without abandoning goals."),

        // ── Leadership ──────────────────────────────────────────────────────
        new("Team Orientation",
            "Leadership",
            "Collaborative behavior, group cohesion building, inclusive decision-making, trust facilitation, prosocial signaling, balancing individual contributions with collective outcomes."),

        new("Strategic Thinking",
            "Leadership",
            "Long-term planning, pattern recognition across complex systems, anticipation of consequences, multi-step reasoning, integration of diverse information for optimal positioning."),

        new("Stress Capacity",
            "Leadership",
            "Functional performance under pressure, emotional regulation during high-stakes situations, cortisol recovery efficiency, maintained cognitive clarity during threat or time-constraint scenarios."),

        // ── Interpersonal ───────────────────────────────────────────────────
        new("Relationship Depth",
            "Interpersonal",
            "Capacity for meaningful emotional bonds, vulnerability tolerance, empathic attunement, sustained relational investment, trust-building through consistent reciprocal engagement."),

        new("Competitive Drive",
            "Interpersonal",
            "Dominance-seeking behavior, status comparison, performance benchmarking against peers, desire to outperform, territorial assertion, zero-sum framing of social interactions."),

        new("Persuasion",
            "Interpersonal",
            "Influence capacity, narrative construction for behavioral change, emotional resonance calibration, credibility establishment, adaptive communication for audience-specific impact."),

        // ── Adaptability ────────────────────────────────────────────────────
        new("Context Switching",
            "Adaptability",
            "Cognitive flexibility across domains, rapid role transitions, working memory management during task alternation, reduced perseveration, fluid adaptation to changing environmental demands."),

        new("Problem Framing",
            "Adaptability",
            "Ability to redefine challenges from multiple perspectives, divergent thinking, constraint relaxation, creative reinterpretation of obstacles as opportunities, non-linear solution pathways."),

        new("Knowledge Transfer",
            "Adaptability",
            "Application of learned patterns across novel domains, analogical reasoning, abstraction of principles from specific instances, cross-domain integration of expertise."),

        // ── Sustainability ──────────────────────────────────────────────────
        new("Balance & Recovery",
            "Sustainability",
            "Homeostatic regulation, work-rest cycling, parasympathetic activation capacity, boundary maintenance between performance and recuperation, prevention of chronic stress accumulation."),

        new("Resilience",
            "Sustainability",
            "Post-adversity recovery speed, stress inoculation effects, maintained self-efficacy after failure, adaptive coping strategy deployment, neuroplastic reorganization after disruption."),

        new("Motivation Source",
            "Sustainability",
            "Internal versus external reward dependency, intrinsic satisfaction from mastery, autonomy-driven engagement, purpose alignment, sustainable energy sourcing versus reliance on external validation."),
    ];

    /// <summary>
    /// Canonical mapping of all 27 chemicals to their biochemical layer.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ChemicalToLayer =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // neurotransmitter (7)
            ["dopamine"] = "neurotransmitter",
            ["serotonin"] = "neurotransmitter",
            ["norepinephrine"] = "neurotransmitter",
            ["gaba"] = "neurotransmitter",
            ["acetylcholine"] = "neurotransmitter",
            ["endocannabinoid"] = "neurotransmitter",
            ["glutamate"] = "neurotransmitter",
            // hormone (10)
            ["cortisol"] = "hormone",
            ["testosterone"] = "hormone",
            ["estradiol"] = "hormone",
            ["progesterone"] = "hormone",
            ["thyroid"] = "hormone",
            ["adrenaline"] = "hormone",
            ["melatonin"] = "hormone",
            ["dhea"] = "hormone",
            ["prolactin"] = "hormone",
            ["oxytocin_h"] = "hormone",
            // peptide (10)
            ["oxytocin"] = "peptide",
            ["vasopressin"] = "peptide",
            ["endorphins"] = "peptide",
            ["enkephalins"] = "peptide",
            ["dynorphin"] = "peptide",
            ["substance_p"] = "peptide",
            ["crh"] = "peptide",
            ["npy"] = "peptide",
            ["bdnf"] = "peptide",
            ["orexin"] = "peptide",
        };
}
