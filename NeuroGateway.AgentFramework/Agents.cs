using NeuroGateway.Models;
using NeuroGateway.Utils;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Agent generation: creates custom agent teams via LLM from personality profiles or professional roles.
/// </summary>
public class Agents
{
    private readonly LlmService _llm;
    private readonly PromptConfig _prompts;
    private readonly string _personalityAgentPrompt;
    private readonly string _roleAgentPrompt;

    public Agents(LlmService llm)
    {
        _llm = llm;
        _prompts = ConfigLoader.LoadJson<PromptConfig>("Prompts.json");
        _personalityAgentPrompt = ConfigLoader.LoadPromptText("PersonalityAgentGeneration.txt");
        _roleAgentPrompt = ConfigLoader.LoadPromptText("RoleAgentGeneration.txt");
    }

    /// <summary>
    /// Generate agents from either a personality profile or a professional role.
    /// </summary>
    public async Task<List<CustomAgent>?> GenerateAgentsAsync(int agentCount, PersonalityProfile? profile = null, string? role = null)
    {
        string prompt;
        string synthName;
        string synthStyle;
        List<string> synthResponsibilities;

        if (profile != null)
        {
            var traitsDescription = profile.Entries.Count > 0
                ? string.Join("\n", profile.Entries.Select(e =>
                    $"- {e.Content} (linked to {e.AllChemicals()})"))
                : "No specific traits recorded yet - generate agents based on a general balanced personality.";

            prompt = _personalityAgentPrompt
                .Replace("{agentCount}", (agentCount - 1).ToString())
                .Replace("{person}", profile.Person)
                .Replace("{traitsDescription}", traitsDescription)
                .Replace("{agentJsonExample}", _prompts.AgentJsonExample);

            synthName = $"{profile.Person}'s Integrator";
            synthResponsibilities = [
                "Integrate all agent perspectives into a cohesive understanding",
                $"Relate conclusions back to {profile.Person}'s personality traits",
                "Identify common themes and actionable insights",
                "Provide an executive summary honoring all viewpoints"
            ];
            synthStyle = "integrative, personality-aware, draws meaningful conclusions. End with CONCLUSION: followed by key insights.";
        }
        else if (role != null)
        {
            var roleExamples = string.Join("\n", _prompts.RoleExamples.Select(kv =>
                $"            - \"{kv.Key}\": {string.Join(", ", kv.Value)}"));

            prompt = _roleAgentPrompt
                .Replace("{agentCount}", (agentCount - 1).ToString())
                .Replace("{role}", role)
                .Replace("{agentJsonExample}", _prompts.AgentJsonExample)
                .Replace("{roleExamples}", roleExamples);

            synthName = $"{role} Team Lead";
            synthResponsibilities = [
                "Integrate all team perspectives into actionable recommendations",
                "Identify consensus and highlight key disagreements",
                "Prioritize insights based on impact and feasibility",
                "Provide an executive summary with clear next steps"
            ];
            synthStyle = $"decisive, experienced {role} leader who synthesizes team input. End with CONCLUSION: followed by key decisions and action items.";
        }
        else
        {
            return null;
        }

        var agents = await CallLlmForAgentsAsync(prompt, agentCount);
        if (agents == null) return null;

        agents.Add(new CustomAgent("Synthesizer", synthName, synthResponsibilities, synthStyle, 300, true));
        return agents;
    }

    public static Dictionary<string, AgentProfile> ToAgentProfiles(List<CustomAgent> agents)
        => agents.ToDictionary(a => a.Name, a => new AgentProfile
        {
            Role = a.Role, Responsibilities = a.Responsibilities,
            Style = a.Style, MaxWords = a.MaxWords, Conclusion = a.IsSynthesizer
        });

    private async Task<List<CustomAgent>?> CallLlmForAgentsAsync(string prompt, int agentCount)
    {
        try
        {
            var responseText = await _llm.AskAsync(prompt);
            Console.WriteLine($"[Agents] Raw LLM response length: {responseText?.Length ?? 0}");
            Console.WriteLine($"[Agents] First 500 chars: {responseText?[..Math.Min(500, responseText.Length)]}");

            var agents = ParseService.ParseJsonArray<List<CustomAgent>>(responseText ?? "");

            if (agents == null || agents.Count == 0)
            {
                Console.WriteLine("[Agents] ParseJsonArray returned null or empty");
                return null;
            }

            Console.WriteLine($"[Agents] Parsed {agents.Count} agents successfully");
            return agents.Count > agentCount - 1 ? agents.Take(agentCount - 1).ToList() : agents;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Agents] Exception: {ex.Message}");
            return null;
        }
    }
}
