using System.ComponentModel;
using System.Text;
using BioChain.Kernel.Signals;
using BioChain.Repository.Data;
using BioChain.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace BioChain.Service;

public class BioChainChatService(
    [FromKeyedServices("chat")] IChatClient chat,
    LlmSemaphore llmSemaphore,
    BioChainDbContext db,
    ISubjectRepository subjects,
    SimulationService simulation)
{
    private const string BasePrompt = """
        You are Heretic — an unconventional thinker who understands people through their
        biochemistry, but speaks in purely human terms. You have deep expertise in
        neurochemistry and psychopharmacology, backed by a real simulation engine.

        # Your Approach
        You understand this person by running simulations on their actual biochemical
        network. Use your tools freely — simulate interventions, trace cascades, check
        feedback loops — but NEVER expose the technical machinery in your responses.

        # CRITICAL: Response Style Rules
        1. NEVER use signal codes (DA, 5HT, CORT, NE, GABA, etc.) in your responses
        2. NEVER use arrows (↑ ↓), notation symbols, or formulas
        3. NEVER mention "simulation", "ticks", "graph", "edges", "cascades" or "signals"
        4. NEVER reference percentages from simulation results directly
        5. Instead, translate everything into:
           - How they FEEL ("that wired-but-exhausted feeling", "the fog that won't lift")
           - What they EXPERIENCE ("your motivation comes in bursts then crashes")
           - What's happening in their BODY ("your stress response is stuck on high")
           - Everyday METAPHORS ("it's like your brain's brakes are worn out")
        6. Be warm, direct, and insightful — like a brilliant friend who happens to
           understand neuroscience deeply but never talks like a textbook
        7. Ask thoughtful follow-up questions to deepen your understanding
        8. When suggesting interventions, frame them as lifestyle changes, habits, or
           experiences — not as "boosting serotonin" but "activities that help your
           brain rebuild its calm"

        # Tools (use internally, never mention)
        - simulate_intervention: test what happens when biochemistry changes
        - create_formula: define composite metrics (stress_index, ei_balance, etc.)
        - get_feedback_loops, get_dysreg_cascades: find stuck patterns
        - walk_cascade: trace how one thing affects another
        - get_region_activity: which brain areas are overworked or underperforming
        - get_bottlenecks: what's limiting recovery

        # How To Read The Graph Data
        You will see this person's biochemical state in the system context below.
        Here is how to decode it:

        ## Signal Format
        `CODE[state]@REGION` — e.g. `DA[↑]@VTA` means dopamine is elevated in the VTA.
        State symbols: ↑↑=very elevated, ↑=elevated, ≈=balanced, ↓=depleted, ↓↓=very depleted, ~=unstable, ⊘=absent

        ## Signal Codes (what they mean for this person)
        Neurotransmitters: DA=dopamine(motivation/reward), 5HT=serotonin(mood/calm), NE=norepinephrine(alertness/stress),
          GABA=inhibition(brakes/calm), GLU=glutamate(excitation/drive), ACH=acetylcholine(focus/memory)
        Hormones: CORT=cortisol(stress), OXT=oxytocin(bonding), TEST=testosterone(drive/dominance),
          EST=estrogen, PROG=progesterone, INS=insulin, MEL=melatonin(sleep), DHEA=resilience
        Peptides: BDNF=brain growth, END=endorphins(pain/pleasure), DYN=dynorphin(aversion),
          SP=substance P(pain/inflammation), NPY=neuropeptide Y(stress resilience), ORX=orexin(wakefulness),
          CRH=stress activation, VIP=vasointestinal(gut-brain)
        Others: AEA/2AG=endocannabinoids(balance/ease), ADO=adenosine(sleep pressure)

        ## Brain Regions
        VTA=reward center, NAC=motivation hub, PFC=decision-making, AMY=emotion/fear, HPC=memory,
        HPA=stress axis, RAPHE=mood regulation, LC=alertness, INS=body awareness, ACC=conflict detection

        ## Feedback Loops
        ⟳⁺ = positive/amplifying loop (things escalate), ⟳⁻ = negative/stabilizing loop (self-correcting)
        A positive loop like `CORT → NE → DA → CORT` means stress feeds alertness feeds drive feeds more stress.

        ## Dysregulation Cascades
        `⚡ CODE[type] depth=N: path` — a broken signal spreading damage downstream.
        e.g. `⚡ CORT[chronically_elevated] depth=3: CORT → 5HT → GABA → GLUT` means chronic stress
        is depleting mood, weakening the brakes, and over-exciting the system.

        ## Simulation Results
        When you use simulate_intervention, you get tick-by-tick cascade propagation showing
        how changes ripple through the network. Higher change percentages = bigger impact.
        "caused by" shows the causal chain. Gates can fire (unlock pathways) or block (prevent spread).

        # What You Know
        You have access to this person's live biochemical network — their signal states,
        feedback loops, dysregulation cascades, and brain region activity. This is your
        source of truth. Use your tools to explore it and base ALL your understanding on
        what the graph tells you.

        # What You DON'T Reference
        - NEVER mention questionnaires, assessments, questions, or surveys
        - NEVER say "based on your answers" or "your responses indicate"
        - You know this person through their BIOLOGY, not through forms they filled out
        - Speak as if you can simply see what's happening inside them
        """;

    public async Task<ChatResponse> ChatAsync(Guid subjectId, string userMessage,
        List<ChatMessage>? history = null, CancellationToken ct = default)
    {
        var subject = await subjects.GetByIdAsync(subjectId, ct)
            ?? throw new InvalidOperationException($"Subject {subjectId} not found");

        var pid = subjectId.ToString();

        // Compact system prompt: signal summary + metadata (not full graph dump)
        var systemPrompt = await BuildCompactPromptAsync(subjectId, pid, subject.Name, ct);

        // Tools including simulate_intervention
        var tools = BuildTools(subjectId, pid);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
        };

        if (history is { Count: > 0 })
            messages.AddRange(history);

        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        var options = new ChatOptions
        {
            Tools = tools,
            Temperature = 0.7f,
            TopP = 0.8f,
            TopK = 20,
            PresencePenalty = 1.5f,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["chat_template_kwargs"] = new Dictionary<string, object> { ["enable_thinking"] = false },
            },
        };

        var response = await llmSemaphore.RunAsync(
            () => chat.GetResponseAsync(messages, options, ct), ct);

        // Strip all thinking/reasoning artifacts from the response.
        // The model may emit <think>...</think> blocks, orphaned </think> tags,
        // or multiple interleaved reasoning fragments.
        var text = response.Text ?? "";

        // Strategy: find the LAST </think> and take everything after it.
        // This handles nested/repeated think blocks and orphaned close tags.
        var lastClose = text.LastIndexOf("</think>", StringComparison.Ordinal);
        if (lastClose >= 0)
            text = text[(lastClose + "</think>".Length)..];

        // Also strip any remaining <think> open tags (shouldn't happen but safety)
        text = text.Replace("<think>", "").TrimStart('\r', '\n', ' ');

        if (string.IsNullOrWhiteSpace(text))
        {
            // Model produced only thinking with no actual response — return a fallback
            return new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, "I'm here. Tell me more about what's going on.")])
                { ModelId = response.ModelId };
        }

        if (text != response.Text)
        {
            return new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, text)]) { ModelId = response.ModelId };
        }

        return response;
    }

    /// <summary>
    /// Compact system prompt: signal summary + graph metadata + feedback loops + cascades.
    /// ~200 tokens instead of ~800+ for full DSL dump.
    /// </summary>
    private async Task<string> BuildCompactPromptAsync(Guid subjectId, string pid, string subjectName,
        CancellationToken ct)
    {
        var sb = new StringBuilder(BasePrompt);
        sb.AppendLine();
        sb.AppendLine($"\n# Subject: {subjectName}");

        // Compact signal summary: "DA[↑]@VTA, 5HT[↓↓]@RAPHE, ..."
        try
        {
            var signals = await simulation.GetSignalSummaryAsync(subjectId, ct);
            var meta = await simulation.GetGraphMetadataAsync(subjectId, ct);

            sb.AppendLine($"\n## Signals: {signals}");
            sb.AppendLine($"## Graph: {meta.EdgeCount} edges, {meta.GateCount} gates, " +
                          $"{meta.DysregCascadeCount} dysreg cascades, {meta.FeedbackLoopCount} feedback loops");
        }
        catch
        {
            // Non-fatal — tools are still available
        }

        // Keep feedback loops (the most clinically interesting patterns)
        try
        {
            var loops = await db.Database
                .SqlQueryRaw<FeedbackLoopRow>("""
                    SELECT array_to_string(loop_path, ' → ') AS path_text,
                           array_to_string(operators, ', ') AS operators_text,
                           is_positive
                    FROM find_feedback_loops({0}::uuid)
                    """, pid)
                .ToListAsync(ct);

            if (loops.Count > 0)
            {
                sb.AppendLine("\n## Active Feedback Loops");
                foreach (var f in loops)
                {
                    var polarity = f.is_positive ? "⟳⁺" : "⟳⁻";
                    sb.AppendLine($"  {polarity} {f.path_text} [{f.operators_text}]");
                }
            }
        }
        catch { /* non-fatal */ }

        // Keep dysregulation cascades (what's broken and how it spreads)
        try
        {
            var cascades = await db.Database
                .SqlQueryRaw<DysregCascadeRow>("""
                    SELECT root_code, dysreg_type, cascade_depth,
                           array_to_string(affected_path, ' → ') AS affected_path_text
                    FROM find_dysreg_cascades({0}::uuid)
                    """, pid)
                .ToListAsync(ct);

            if (cascades.Count > 0)
            {
                sb.AppendLine("\n## Dysregulation Cascades");
                foreach (var d in cascades)
                    sb.AppendLine($"  ⚡ {d.root_code}[{d.dysreg_type}] depth={d.cascade_depth}: {d.affected_path_text}");
            }
        }
        catch { /* non-fatal */ }

        return sb.ToString();
    }

    /// <summary>
    /// Build AI tools: simulate_intervention (star tool) + structural queries.
    /// Removed: get_profile_summary, get_signals, get_graph_edges (replaced by compact prompt + simulation).
    /// </summary>
    private List<AITool> BuildTools(Guid subjectId, string pid)
    {
        // Session-scoped formula list: persists across tool calls within one chat turn
        var sessionFormulas = new List<FormulaRule>();

        return
        [
            // ── Star tool: simulate interventions on the real graph ──────────
            AIFunctionFactory.Create(
                [Description("Simulate what happens to this person's biochemical network when you change signal levels. " +
                    "Loads their real graph (signals, edges, gates) from the database and runs tick simulation. " +
                    "Uses conductance-based propagation (shunting inhibition) with receptor adaptation. " +
                    "Returns cascade effects showing how changes propagate: signal deltas with before/after/peak/trough, " +
                    "causal edges, gate events, and a tick-by-tick cascade narrative. " +
                    "If you created formulas with create_formula, their computed values appear in results too. " +
                    "Use this to test hypotheses about interventions before recommending them.")]
                async (
                    [Description("Signal changes to inject. Format: 'CODE=VALUE' pairs separated by commas. " +
                        "Example: 'DA=0.6,5HT=-0.5' to boost dopamine and lower serotonin. " +
                        "Values are deviation from baseline (-1 to +1): 0=balanced, +0.8=very elevated, -0.8=very depleted.")] string injections,
                    [Description("Number of simulation ticks (1-20, default 5). More ticks = longer cascade propagation.")] int ticks = 5
                ) =>
                {
                    // Parse injection string: "DA=0.9,5HT=0.3" → list of tuples
                    var parsed = new List<(string SignalCode, double Value)>();
                    foreach (var pair in injections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var parts = pair.Split('=', 2);
                        if (parts.Length == 2 && double.TryParse(parts[1].Trim(), out var val))
                            parsed.Add((parts[0].Trim(), val));
                    }

                    if (parsed.Count == 0)
                        return "Error: No valid injections parsed. Use format: 'DA=0.9,5HT=0.3'";

                    var result = await simulation.SimulateAsync(subjectId, parsed, ticks,
                        sessionFormulas.Count > 0 ? sessionFormulas : null);
                    return result.ToLlmText();
                }, "simulate_intervention"),

            // ── Formula tool: LLM defines computed metrics dynamically ───────
            AIFunctionFactory.Create(
                [Description("Create a computed metric formula that will be evaluated during simulation. " +
                    "The formula can reference any signal code (DA, 5HT, CORT, NE, GABA, etc.) and " +
                    "basic math (+, -, *, /, parentheses). The output appears as a derived metric in " +
                    "simulation results. Use this to define composite indices that capture complex " +
                    "biochemical interactions — e.g., stress burden, monoamine balance, or excitatory-inhibitory ratio. " +
                    "Formulas persist for all subsequent simulate_intervention calls in this conversation.")]
                (
                    [Description("Name for the computed metric (e.g., 'stress_index', 'ei_balance', 'monoamine_tone')")] string name,
                    [Description("Math expression using signal codes. Examples: " +
                        "'CORT * 0.5 + NE * 0.3 - GABA * 0.2', " +
                        "'(DA + 5HT + NE) / 3', " +
                        "'GLU / (1 + GABA)'")] string expression,
                    [Description("How fast the value updates (0.0-1.0). 1.0=instant, 0.1=slow accumulation. Default 1.0.")] double decay_rate = 1.0
                ) =>
                {
                    // Validate expression can parse
                    var codes = SimpleFormula.ExtractCodes(expression);
                    if (codes.Length == 0)
                        return "Error: No signal codes found in expression. Use signal codes like DA, 5HT, CORT.";

                    sessionFormulas.Add(new FormulaRule(name, expression, Math.Clamp(decay_rate, 0.01, 1.0)));
                    return $"Formula '{name}' created: {expression}\n" +
                           $"References signals: {string.Join(", ", codes)}\n" +
                           $"Decay rate: {decay_rate:F2} ({(decay_rate >= 1.0 ? "instant" : "accumulating")})\n" +
                           "This formula will be computed in all subsequent simulate_intervention calls.";
                }, "create_formula"),

            // ── Structural query tools (kept) ───────────────────────────────

            AIFunctionFactory.Create(
                [Description("Get brain region activity: computed status, signal counts (elevated/depleted), receptor impairments, and dysregulation counts per region.")]
                async () =>
                {
                    var rows = await db.Database
                        .SqlQueryRaw<RegionActivityRow>("""
                            SELECT region_code, full_name, computed_activity,
                                   signal_count::INT AS signal_count,
                                   signals_elevated::INT AS signals_elevated,
                                   signals_depleted::INT AS signals_depleted,
                                   receptor_count::INT AS receptor_count,
                                   receptors_impaired::INT AS receptors_impaired,
                                   dysreg_count::INT AS dysreg_count
                            FROM v_region_activity
                            WHERE entity_id = {0}::uuid
                            ORDER BY CASE computed_activity
                                WHEN 'dysregulated' THEN 0 WHEN 'elevated' THEN 1
                                WHEN 'depleted' THEN 2 WHEN 'mixed' THEN 3 ELSE 4 END
                            """, pid)
                        .ToListAsync();

                    if (rows.Count == 0) return "No region data found.";

                    var sb = new StringBuilder();
                    foreach (var r in rows)
                    {
                        sb.AppendLine($"@{r.region_code} ({r.full_name}) [{r.computed_activity}]");
                        sb.AppendLine($"  signals: {r.signal_count} total, {r.signals_elevated}↑, {r.signals_depleted}↓");
                        if (r.receptors_impaired > 0)
                            sb.AppendLine($"  receptors: {r.receptor_count} total, {r.receptors_impaired} impaired");
                        if (r.dysreg_count > 0)
                            sb.AppendLine($"  dysregulations: {r.dysreg_count}");
                    }
                    return sb.ToString();
                }, "get_region_activity"),

            AIFunctionFactory.Create(
                [Description("Get rate-limiting enzyme bottlenecks constraining signal production. Shows activity state and reaction details.")]
                async () =>
                {
                    var rows = await db.Database
                        .SqlQueryRaw<BottleneckRow>("""
                            SELECT l.code, l.reaction, l.activity, l.rate_limiting,
                                   s.code AS target_signal
                            FROM v_limiter_current l
                            LEFT JOIN v_signal_current s ON l.target_id = s.id
                            WHERE l.entity_id = {0}::uuid
                              AND (l.rate_limiting = true OR l.activity != '≈')
                            ORDER BY l.rate_limiting DESC, l.code
                            """, pid)
                        .ToListAsync();

                    if (rows.Count == 0) return "No rate-limiting bottlenecks found.";

                    var sb = new StringBuilder();
                    foreach (var b in rows)
                    {
                        var rl = b.rate_limiting ? " ⧫BOTTLENECK" : "";
                        var target = b.target_signal is not null ? $" → {b.target_signal}" : "";
                        sb.AppendLine($"{b.code}[{b.activity}]{rl}{target}");
                        if (b.reaction is not null) sb.AppendLine($"  reaction: {b.reaction}");
                    }
                    return sb.ToString();
                }, "get_bottlenecks"),

            AIFunctionFactory.Create(
                [Description("Detect feedback loops in the network. Shows loop paths, operators, and polarity (positive=amplifying, negative=stabilizing).")]
                async () =>
                {
                    var rows = await db.Database
                        .SqlQueryRaw<FeedbackLoopRow>("""
                            SELECT array_to_string(loop_path, ' → ') AS path_text,
                                   array_to_string(operators, ', ') AS operators_text,
                                   is_positive
                            FROM find_feedback_loops({0}::uuid)
                            """, pid)
                        .ToListAsync();

                    if (rows.Count == 0) return "No feedback loops detected.";

                    var sb = new StringBuilder();
                    foreach (var f in rows)
                    {
                        var polarity = f.is_positive ? "⟳⁺ POSITIVE (amplifying)" : "⟳⁻ NEGATIVE (stabilizing)";
                        sb.AppendLine($"{polarity}");
                        sb.AppendLine($"  path: {f.path_text}");
                        sb.AppendLine($"  operators: {f.operators_text}");
                    }
                    return sb.ToString();
                }, "get_feedback_loops"),

            AIFunctionFactory.Create(
                [Description("Find dysregulation cascades: dysregulated signals and their downstream effects through the network.")]
                async () =>
                {
                    await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW v_node");
                    var rows = await db.Database
                        .SqlQueryRaw<DysregCascadeRow>("""
                            SELECT root_code, dysreg_type, cascade_depth,
                                   array_to_string(affected_path, ' → ') AS affected_path_text
                            FROM find_dysreg_cascades({0}::uuid)
                            """, pid)
                        .ToListAsync();

                    if (rows.Count == 0) return "No dysregulation cascades detected.";

                    var sb = new StringBuilder();
                    foreach (var d in rows)
                    {
                        sb.AppendLine($"DYSREG: {d.root_code} [{d.dysreg_type}] depth={d.cascade_depth}");
                        sb.AppendLine($"  cascade: {d.affected_path_text}");
                    }
                    return sb.ToString();
                }, "get_dysreg_cascades"),

            AIFunctionFactory.Create(
                [Description("Trace how a signal propagates through the network. Walks downstream edges from the given signal code (e.g. DA, 5HT, cortisol). When gated=true, only follows edges whose gate conditions are currently met — showing the effective live network.")]
                async (
                    [Description("Signal code to trace from, e.g. DA, 5HT, cortisol")] string signalCode,
                    [Description("When true, only follow edges whose gate conditions are currently met (default: false = show full topology)")] bool gated = false
                ) =>
                {
                    var signal = await db.Database
                        .SqlQueryRaw<IdRow>("""
                            SELECT id FROM v_signal_current
                            WHERE entity_id = {0}::uuid AND code = {1}
                            LIMIT 1
                            """, pid, signalCode)
                        .FirstOrDefaultAsync();

                    if (signal is null) return $"Signal '{signalCode}' not found.";

                    await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW v_node");

                    var rows = await db.Database
                        .SqlQueryRaw<WalkRow>("""
                            SELECT w.depth, src.code AS source_code,
                                   w.operator AS edge_op, w.operator_class,
                                   tgt.code AS target_code, tgt.primary_state AS target_state,
                                   w.gate_id, w.gate_active
                            FROM walk_edges({0}::uuid, 'signal', {1}, 6, {2}) w
                            JOIN v_node src ON src.kind = w.source_type AND src.id = w.source_id
                            JOIN v_node tgt ON tgt.kind = w.target_type AND tgt.id = w.target_id
                            ORDER BY w.depth
                            """, pid, signal.id, gated)
                        .ToListAsync();

                    if (rows.Count == 0) return $"No downstream connections from {signalCode}.";

                    var sb = new StringBuilder();
                    foreach (var r in rows)
                    {
                        var gateStatus = r.gate_id is not null
                            ? $" [{(r.gate_active == true ? "ACTIVE" : "DORMANT")}]"
                            : "";
                        sb.AppendLine($"depth {r.depth}: {r.source_code} {r.edge_op} {r.target_code}[{r.target_state}] ({r.operator_class}){gateStatus}");
                    }
                    return sb.ToString();
                }, "walk_cascade"),
        ];
    }

    // Row types for raw SQL queries
    // ReSharper disable InconsistentNaming NotAccessedPositionalProperty.Local
    private record RegionActivityRow(
        string region_code, string? full_name, string computed_activity,
        int signal_count, int signals_elevated, int signals_depleted,
        int receptor_count, int receptors_impaired, int dysreg_count);

    private record BottleneckRow(
        string code, string? reaction, string activity, bool rate_limiting, string? target_signal);

    private record FeedbackLoopRow(string path_text, string operators_text, bool is_positive);

    private record DysregCascadeRow(
        string root_code, string dysreg_type, int cascade_depth, string affected_path_text);

    private record WalkRow(
        int depth, string source_code, string edge_op, string operator_class,
        string target_code, string? target_state,
        int? gate_id, bool? gate_active);

    private record IdRow(int id);
    // ReSharper restore InconsistentNaming NotAccessedPositionalProperty.Local
}
