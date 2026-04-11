namespace BioChain.Service;

/// <summary>
/// Tool definitions in OpenAI function calling format for agentic chat.
/// </summary>
internal static class ToolRegistry
{
    public static readonly object[] Definitions =
    [
        new
        {
            type = "function",
            function = new
            {
                name = "simulate",
                description = "Run a biochemical simulation on the program's network. Apply perturbations (e.g. drug interventions) and observe how the signaling network propagates changes over time. Use this when the user asks 'what happens if we give this drug' or 'what effect would X have'.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["perturbations"] = new
                        {
                            type = "array",
                            description = "List of perturbations to apply (drug interventions, concentration changes)",
                            items = new
                            {
                                type = "object",
                                properties = new Dictionary<string, object>
                                {
                                    ["target_code"] = new { type = "string", description = "Node code to perturb (e.g. 'L.nt:5HT', 'R:5HT1A')" },
                                    ["target_region"] = new { type = "string", description = "Brain region (e.g. 'DRN', 'PFC', 'NAc')" },
                                    ["action"] = new { type = "string", description = "Perturbation type: 'block', 'enhance', 'set_concentration'" },
                                    ["value"] = new { type = "number", description = "Optional numeric value for concentration changes" }
                                },
                                required = new[] { "target_code", "target_region", "action" }
                            }
                        },
                        ["max_ticks"] = new { type = "integer", description = "Maximum simulation ticks (default 1000)" }
                    },
                    required = new[] { "perturbations" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_program_state",
                description = "Get the current state of the program's biochemical network. Shows all nodes, edges, and diagnostics. Use this to inspect the network before or after simulation.",
                parameters = new { type = "object", properties = new Dictionary<string, object>() }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "search_nodes",
                description = "Search for specific nodes in the network by their code pattern. Use to find receptors, ligands, kinases, etc.",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["code_pattern"] = new { type = "string", description = "Substring to match against node codes (e.g. '5HT', 'GABA', 'BDNF')" }
                    },
                    required = new[] { "code_pattern" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_simulation_results",
                description = "Get results from previous simulation runs on this program. Shows perturbations applied, tick counts, and status.",
                parameters = new { type = "object", properties = new Dictionary<string, object>() }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "predict_plasticity",
                description = "Run the PLASTICITY analysis pipeline. Projects temporal changes (Δ0→Δ3) from the current BASE state — receptor desensitization, structural remodeling, protocol rewiring. Use when asked about long-term effects or disease progression.",
                parameters = new { type = "object", properties = new Dictionary<string, object>() }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "infer_meta_programs",
                description = "Run the META analysis pipeline. Infers developmental/epigenetic programs — what the system thinks 'normal' is (σ̃ setpoints), methylation locks (⊲̃), structural programs (∫̃). Use when asked about treatment resistance or why the system is 'stuck'.",
                parameters = new { type = "object", properties = new Dictionary<string, object>() }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "compute_convergence",
                description = "Run the CONVERGENCE analysis pipeline. Full prognosis — trajectories (⊳), convergence diagnostics (∮), and clinical flags (⚡). Use when asked about outlook, prognosis, or treatment planning.",
                parameters = new { type = "object", properties = new Dictionary<string, object>() }
            }
        },
    ];
}
