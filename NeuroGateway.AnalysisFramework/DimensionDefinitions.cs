namespace NeuroGateway.AnalysisFramework;

public static class DimensionDefinitions
{
    public enum ScoringMode { Work, Private }

    public sealed record DimensionDef(
        string Name,
        string Section,
        string Category,
        string Description,
        /// <summary>
        /// Chemicals that are strong positive indicators for this dimension.
        /// Weight 1.0 = primary driver, 0.5 = secondary contributor.
        /// Used for chemical affinity scoring independent of embedding similarity.
        /// </summary>
        IReadOnlyDictionary<string, float> ChemicalAffinity,
        /// <summary>
        /// Mode relevance multiplier: how much this dimension matters in Work vs Private context.
        /// 1.0 = fully relevant, 0.3 = weakly relevant (still scored but compressed).
        /// </summary>
        float WorkRelevance = 1.0f,
        float PrivateRelevance = 1.0f)
    {
        public float GetModeMultiplier(ScoringMode mode) => mode switch
        {
            ScoringMode.Work => WorkRelevance,
            ScoringMode.Private => PrivateRelevance,
            _ => 1.0f
        };
    }

    public static readonly IReadOnlyList<DimensionDef> All =
    [
        // ═══════════════════════════════════════════════════════════════════
        // BEHAVIORAL DIMENSIONS — professional/work traits
        // ═══════════════════════════════════════════════════════════════════

        // ── Drive & Trajectory ─────────────────────────────────────────────
        new("Ambition",
            "Behavioral", "Drive & Trajectory",
            "Relentless pursuit of career advancement, promotion-seeking, expanding professional authority and scope. Taking on stretch assignments, volunteering for high-visibility projects, negotiating for larger responsibilities. Building professional brand and reputation deliberately. Setting aggressive personal milestones and KPIs beyond what is required.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["dopamine"] = 1.0f, ["testosterone"] = 0.8f, ["norepinephrine"] = 0.5f,
                ["orexin"] = 0.5f, ["npy"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.4f),

        new("Risk Tolerance",
            "Behavioral", "Drive & Trajectory",
            "Comfort with ambiguity, making decisions with incomplete information, betting on unproven technologies or architectures. Willingness to challenge established practices, propose radical alternatives, or start ventures without guaranteed outcomes. Accepting personal accountability for uncertain bets. Thriving in environments where failure is possible and visible.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["norepinephrine"] = 1.0f, ["endocannabinoid"] = 0.7f, ["dopamine"] = 0.6f,
                ["adrenaline"] = 0.5f, ["testosterone"] = 0.4f
            }, WorkRelevance: 0.9f, PrivateRelevance: 0.5f),

        new("Persistence",
            "Behavioral", "Drive & Trajectory",
            "Sustained effort through monotony, frustration, and repeated setbacks. Debugging for hours without giving up, maintaining legacy codebases, completing compliance work. Staying focused on long-term goals when short-term rewards are absent. Grinding through tedious documentation, regulatory requirements, and thankless infrastructure maintenance.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["gaba"] = 1.0f, ["serotonin"] = 0.8f, ["npy"] = 0.7f,
                ["enkephalins"] = 0.5f, ["dhea"] = 0.4f
            }, WorkRelevance: 0.9f, PrivateRelevance: 0.6f),

        // ── Leadership ─────────────────────────────────────────────────────
        new("Team Orientation",
            "Behavioral", "Leadership",
            "Prioritizing group success over individual recognition. Active mentoring, code reviewing with genuine care for growth. Sharing credit, amplifying others' contributions, investing time in pair programming and knowledge sharing. Building psychological safety in teams, facilitating inclusive standups and retrospectives, protecting junior members from blame.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["oxytocin"] = 1.0f, ["oxytocin_h"] = 0.9f, ["vasopressin"] = 0.7f,
                ["prolactin"] = 0.6f, ["serotonin"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.5f),

        new("Strategic Thinking",
            "Behavioral", "Leadership",
            "Multi-quarter planning, technology roadmapping, system architecture decisions that account for organizational constraints. Recognizing patterns across distributed systems, anticipating second-order effects of technical choices. Balancing technical debt against delivery pressure. Making trade-off decisions that consider business context, team capacity, and long-term maintenance.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["acetylcholine"] = 1.0f, ["glutamate"] = 0.8f, ["thyroid"] = 0.6f,
                ["bdnf"] = 0.5f, ["serotonin"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.3f),

        new("Stress Capacity",
            "Behavioral", "Leadership",
            "Maintaining clear decision-making during production incidents, security breaches, tight deadlines. Recovering quickly from high-pressure situations without accumulated burnout. Managing stakeholder expectations during crises. Functioning effectively when multiple urgent priorities compete simultaneously. Absorbing organizational stress without passing it to the team.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["cortisol"] = 1.0f, ["dhea"] = 0.9f, ["adrenaline"] = 0.7f,
                ["gaba"] = 0.5f, ["npy"] = 0.5f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.6f),

        // ── Execution ──────────────────────────────────────────────────────
        new("Competitive Drive",
            "Behavioral", "Execution",
            "Drive to outperform peers, benchmarking against industry standards, pushing for best-in-class solutions. Assertiveness in technical debates, salary negotiations, performance reviews. Urgency to deliver faster or better than competing teams. Measuring personal output and wanting to be recognized as a top performer.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["testosterone"] = 1.0f, ["dopamine"] = 0.7f, ["norepinephrine"] = 0.6f,
                ["adrenaline"] = 0.5f, ["orexin"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.3f),

        new("Context Switching",
            "Behavioral", "Execution",
            "Fluid movement between concurrent projects, meetings, code reviews without cognitive overhead. Managing multiple workstreams with different stakeholders simultaneously. Shifting between deep technical work and collaborative communication rapidly. Maintaining quality across parallel responsibilities without dropping threads.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["acetylcholine"] = 1.0f, ["dopamine"] = 0.6f, ["norepinephrine"] = 0.6f,
                ["thyroid"] = 0.4f, ["orexin"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.3f),

        new("Problem Solving",
            "Behavioral", "Execution",
            "Novel debugging approaches, creative architectural solutions for unfamiliar constraints. Lateral thinking when standard approaches fail. Breaking down complex problems into tractable subproblems. Connecting insights across different domains and technology stacks. Finding elegant solutions that simplify rather than add complexity.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["glutamate"] = 1.0f, ["bdnf"] = 0.8f, ["endocannabinoid"] = 0.6f,
                ["acetylcholine"] = 0.5f, ["dopamine"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.5f),

        // ── Professional Growth ────────────────────────────────────────────
        new("Knowledge Transfer",
            "Behavioral", "Professional Growth",
            "Teaching, mentoring, writing documentation, giving conference talks. Translating complex concepts into accessible explanations. Creating onboarding materials, architecture decision records, technical blog posts. Building shared understanding across teams with different expertise levels. Actively investing in growing others' capabilities.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["bdnf"] = 1.0f, ["glutamate"] = 0.7f, ["acetylcholine"] = 0.6f,
                ["oxytocin"] = 0.5f, ["prolactin"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.4f),

        new("Work-Life Balance",
            "Behavioral", "Professional Growth",
            "Setting and maintaining boundaries between professional and personal life. Disconnecting from work communications outside hours. Recognizing burnout signals and taking preventive action. Prioritizing personal health, relationships, and hobbies alongside career demands. Sustainable pace over heroic sprints.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["melatonin"] = 1.0f, ["gaba"] = 0.8f, ["progesterone"] = 0.6f,
                ["serotonin"] = 0.5f, ["endocannabinoid"] = 0.4f
            }, WorkRelevance: 0.8f, PrivateRelevance: 0.8f),

        new("Career Resilience",
            "Behavioral", "Professional Growth",
            "Bouncing back from job loss, project failures, organizational restructuring. Ability to pivot skills and reinvent professionally after setbacks. Maintaining motivation and professional identity during periods of uncertainty. Learning from failure rather than being defined by it. Building a career that can weather industry disruption.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["dhea"] = 1.0f, ["enkephalins"] = 0.8f, ["orexin"] = 0.7f,
                ["npy"] = 0.6f, ["bdnf"] = 0.4f
            }, WorkRelevance: 1.0f, PrivateRelevance: 0.4f),

        // ═══════════════════════════════════════════════════════════════════
        // PERSONAL DIMENSIONS — emotional/relational/inner traits
        // ═══════════════════════════════════════════════════════════════════

        // ── Emotional Landscape ────────────────────────────────────────────
        new("Emotional Depth",
            "Personal", "Emotional Landscape",
            "Capacity for deep vulnerability and trust in close relationships. Experiencing emotions with full intensity rather than surface-level. Willingness to sit with difficult feelings rather than numbing or avoiding. Rich inner emotional life that informs empathy and connection. Emotional warmth and genuine presence with others.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["oxytocin"] = 1.0f, ["vasopressin"] = 0.7f, ["endorphins"] = 0.7f,
                ["serotonin"] = 0.5f, ["estradiol"] = 0.4f
            }, WorkRelevance: 0.4f, PrivateRelevance: 1.0f),

        new("Emotional Regulation",
            "Personal", "Emotional Landscape",
            "Managing impulsive reactions, controlling anger, moderating anxiety without suppression. Maintaining composure during interpersonal conflict while still feeling the emotion. Processing frustration constructively rather than explosively or through withdrawal. Choosing responses rather than being hijacked by emotional reactivity.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["gaba"] = 1.0f, ["serotonin"] = 0.9f, ["cortisol"] = 0.5f,
                ["progesterone"] = 0.4f, ["endocannabinoid"] = 0.4f
            }, WorkRelevance: 0.7f, PrivateRelevance: 1.0f),

        new("Sensitivity",
            "Personal", "Emotional Landscape",
            "Heightened awareness of others' emotional states, picking up on subtle social cues. Feeling others' pain deeply, being moved by art, music, or stories. Strong empathic attunement that can be both a gift and a burden. Processing criticism or rejection intensely. Mirror-like emotional responsiveness to the environment.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["substance_p"] = 1.0f, ["dynorphin"] = 0.8f, ["oxytocin"] = 0.6f,
                ["crh"] = 0.5f, ["estradiol"] = 0.4f
            }, WorkRelevance: 0.4f, PrivateRelevance: 1.0f),

        // ── Relational Style ───────────────────────────────────────────────
        new("Attachment Security",
            "Personal", "Relational Style",
            "Trusting without excessive anxiety, comfortable with both intimacy and independence. Low jealousy or possessiveness in relationships. Secure bonding that tolerates partner autonomy without triggering abandonment fears. Consistent emotional availability without clinging or avoidance patterns.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["oxytocin"] = 1.0f, ["vasopressin"] = 0.7f, ["endorphins"] = 0.6f,
                ["serotonin"] = 0.5f, ["gaba"] = 0.4f
            }, WorkRelevance: 0.3f, PrivateRelevance: 1.0f),

        new("Intimacy Capacity",
            "Personal", "Relational Style",
            "Openness to physical closeness, eye contact, vulnerable conversations. Sharing deeply personal thoughts and experiences with trusted others. Comfort with emotional and physical nakedness. Nurturing behavior in romantic and familial relationships. Creating safe spaces for mutual vulnerability.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["oxytocin"] = 1.0f, ["prolactin"] = 0.8f, ["estradiol"] = 0.7f,
                ["vasopressin"] = 0.5f, ["endorphins"] = 0.4f
            }, WorkRelevance: 0.2f, PrivateRelevance: 1.0f),

        new("Social Energy",
            "Personal", "Relational Style",
            "Reward from social gatherings, parties, group activities versus need for solitary recharge. Preference for large social networks versus deep one-on-one connections. Energy levels after extended social interaction. Whether stimulation comes from people or from quiet reflection and solo pursuits.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["dopamine"] = 1.0f, ["orexin"] = 0.8f, ["endorphins"] = 0.5f,
                ["melatonin"] = 0.5f, ["serotonin"] = 0.4f
            }, WorkRelevance: 0.6f, PrivateRelevance: 1.0f),

        // ── Inner Drive ────────────────────────────────────────────────────
        new("Self-Awareness",
            "Personal", "Inner Drive",
            "Accurate monitoring of own emotional states and behavioral patterns. Honest self-assessment without defensive distortion. Recognizing personal biases, triggers, and habitual reactions. Growth from personal feedback and self-reflection. Understanding the gap between intention and impact in relationships.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["acetylcholine"] = 1.0f, ["serotonin"] = 0.8f, ["bdnf"] = 0.6f,
                ["gaba"] = 0.4f, ["endocannabinoid"] = 0.3f
            }, WorkRelevance: 0.6f, PrivateRelevance: 1.0f),

        new("Playfulness",
            "Personal", "Inner Drive",
            "Spontaneity, humor, creative expression outside of work. Novelty seeking in hobbies, travel, exploration. Light-hearted social interaction without goal-oriented purpose. Ability to be silly, experiment, and play as an adult. Finding joy in the process rather than only in outcomes.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["endocannabinoid"] = 1.0f, ["dopamine"] = 0.8f, ["endorphins"] = 0.7f,
                ["serotonin"] = 0.3f, ["orexin"] = 0.3f
            }, WorkRelevance: 0.3f, PrivateRelevance: 1.0f),

        new("Purpose & Meaning",
            "Personal", "Inner Drive",
            "Contentment from value-aligned living, spiritual or philosophical practice, community contribution. Life satisfaction independent of external achievement. Finding meaning through caregiving, legacy building, generational connection. Sense of direction that transcends immediate goals and material success.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["serotonin"] = 1.0f, ["npy"] = 0.8f, ["oxytocin"] = 0.6f,
                ["endorphins"] = 0.4f, ["bdnf"] = 0.3f
            }, WorkRelevance: 0.5f, PrivateRelevance: 1.0f),

        // ── Resilience & Recovery ───────────────────────────────────────────
        new("Stress Response",
            "Personal", "Resilience & Recovery",
            "Pattern of activation during personal crises, grief, relationship conflict. Whether the default response is fight, flight, or freeze. How the body processes emotional distress from betrayal, loss, or loneliness. Speed and completeness of physiological return to baseline after acute stress.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["crh"] = 1.0f, ["cortisol"] = 0.8f, ["adrenaline"] = 0.7f,
                ["substance_p"] = 0.6f, ["norepinephrine"] = 0.4f
            }, WorkRelevance: 0.6f, PrivateRelevance: 1.0f),

        new("Healing Capacity",
            "Personal", "Resilience & Recovery",
            "Natural pain relief and emotional wound processing over time. Recovery after trauma, breakups, or major life transitions. Neuroplastic ability to reorganize after loss and build new meaning. Resilience against chronic grief and post-traumatic rumination. Converting painful experiences into wisdom rather than bitterness.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["enkephalins"] = 1.0f, ["endorphins"] = 0.8f, ["bdnf"] = 0.7f,
                ["dhea"] = 0.6f, ["npy"] = 0.4f
            }, WorkRelevance: 0.3f, PrivateRelevance: 1.0f),

        new("Inner Peace",
            "Personal", "Resilience & Recovery",
            "Baseline calm, low resting anxiety, comfort with silence and stillness. Quality of sleep and circadian rhythm stability. Contentment without external stimulation or achievement validation. Ability to simply be without needing to do, produce, or perform. Groundedness that persists through external turbulence.",
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["gaba"] = 1.0f, ["melatonin"] = 0.9f, ["endocannabinoid"] = 0.7f,
                ["serotonin"] = 0.5f, ["progesterone"] = 0.4f
            }, WorkRelevance: 0.4f, PrivateRelevance: 1.0f),
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
