using Microsoft.Extensions.AI;
using Models;

namespace Agents;

/// <summary>
/// Service for generating custom agent teams, running group chats, and analyzing text for personality traits.
/// </summary>
public class AgentService
{
    private readonly LlmService _llm;
    private readonly PromptConfig _prompts;
    private readonly string _personalityAgentPrompt;
    private readonly string _roleAgentPrompt;

    public AgentService(LlmService llm)
    {
        _llm = llm;
        _prompts = ConfigLoader.LoadJson<PromptConfig>("Prompts.json");
        _personalityAgentPrompt = ConfigLoader.LoadPromptText("PersonalityAgentGeneration.txt");
        _roleAgentPrompt = ConfigLoader.LoadPromptText("RoleAgentGeneration.txt");
    }

    /// <summary>
    /// Generate agents from either a personality profile or a professional role.
    /// Pass profile for personality-based, or role for role-based generation.
    /// </summary>
    public async Task<List<CustomAgent>?> GenerateAgentsAsync(int agentCount, PersonalityProfile? profile = null, string? role = null)
    {
        string prompt;
        string synthName;
        string synthStyle;
        List<string> synthResponsibilities;

        if (profile != null)
        {
            var traitsDescription = profile.Traits.Count > 0
                ? string.Join("\n", profile.Traits.Select(t =>
                    $"- {t.Topic}: {t.Explanation} (linked to {t.Neurotransmitter ?? "general"})"))
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

    // ===== Group Chat =====

    /// <summary>
    /// Run a group chat. When synthesizerInstruction is provided, agents run in parallel
    /// and the synthesizer combines their outputs. Without it, agents run sequentially
    /// with shared conversation history.
    /// </summary>
    public async Task<(string FullOutput, string? SynthesizerOutput)> RunGroupChatAsync(
        Dictionary<string, AgentProfile> profiles,
        string topic,
        string? synthesizerInstruction = null,
        int maxIterations = 8)
    {
        var synthesizer = synthesizerInstruction != null
            ? profiles.FirstOrDefault(p => p.Value.Conclusion)
            : default;

        var chatAgents = synthesizer.Key != null
            ? profiles.Where(p => !p.Value.Conclusion).ToDictionary(p => p.Key, p => p.Value)
            : profiles;

        List<(string Name, string Response)> agentOutputs;

        if (synthesizer.Key != null)
        {
            var tasks = chatAgents.Select(async kv =>
            {
                try
                {
                    var response = await _llm.ChatWithProfileAsync(kv.Value, [new(ChatRole.User, topic)]);
                    return (Name: kv.Key, Response: response);
                }
                catch { return (Name: kv.Key, Response: (string?)null); }
            });

            var results = await Task.WhenAll(tasks);
            agentOutputs = results.Where(r => r.Response != null).Select(r => (r.Name, r.Response!)).ToList();
        }
        else
        {
            agentOutputs = [];
            var conversationHistory = new List<ChatMessage> { new(ChatRole.User, topic) };
            var agents = chatAgents.Keys.ToArray();

            for (int i = 0; i < maxIterations && i < agents.Length; i++)
            {
                var name = agents[i % agents.Length];
                var profile = chatAgents[name];

                try
                {
                    var response = await _llm.ChatWithProfileAsync(profile, conversationHistory);
                    agentOutputs.Add((name, response));
                    conversationHistory.Add(new ChatMessage(ChatRole.Assistant, $"[{name}]: {response}"));

                    if (profile.Conclusion && response.Contains("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                        break;
                }
                catch { /* Skip agent on error */ }
            }
        }

        string? synthesizerOutput = null;
        if (synthesizer.Key != null && agentOutputs.Count > 0)
        {
            var synthTopic = $"{topic}\n\nHere are the agent responses:\n\n" +
                             string.Join("\n", agentOutputs.Select(a => $"[{a.Name}]: {a.Response}")) +
                             $"\n\n{synthesizerInstruction}";
            try
            {
                synthesizerOutput = await _llm.ChatWithProfileAsync(synthesizer.Value, [new(ChatRole.User, synthTopic)]);
            }
            catch { /* Synthesizer failed */ }
        }

        var fullOutput = string.Join("\n", agentOutputs.Select(a => a.Response));
        if (synthesizerOutput != null)
            fullOutput += "\n" + synthesizerOutput;

        return (fullOutput, synthesizerOutput);
    }

    /// <summary>
    /// Run 3+1 biochemical layer agents in parallel (NT, hormone, peptide) + synthesizer.
    /// Returns always 4 NeuroResponse objects. The {chemicals} placeholder in agent styles/topic
    /// is replaced with the corresponding profile string.
    /// </summary>
    public async Task<List<NeuroResponse>> RunNeuroRespondAsync(
        Dictionary<string, AgentProfile> profiles,
        string topic,
        string ntProfile, string hormoneProfile, string peptideProfile)
    {
        var synthesizer = profiles.FirstOrDefault(p => p.Value.Conclusion);
        var layerAgents = profiles.Where(p => !p.Value.Conclusion).ToList();

        string ChemicalsFor(string? layer) => layer?.ToLowerInvariant() switch
        {
            "neurotransmitter" => ntProfile, "hormone" => hormoneProfile, "peptide" => peptideProfile, _ => "Unknown"
        };
        static string LabelFor(string? layer) => layer?.ToLowerInvariant() switch
        {
            "neurotransmitter" => "Neurotransmitters", "hormone" => "Hormones", "peptide" => "Peptides", _ => "Unknown"
        };

        var tasks = layerAgents.Select(async kv =>
        {
            var chemicals = ChemicalsFor(kv.Value.Layer);
            var label = LabelFor(kv.Value.Layer);
            try
            {
                var response = await _llm.ChatWithProfileAsync(
                    kv.Value.WithStyle(kv.Value.Style.Replace("{chemicals}", chemicals)),
                    [new(ChatRole.User, topic.Replace("{chemicals}", chemicals))]);
                return new NeuroResponse(label, ResponseService.ExtractSuggestion(response) ?? response.Trim());
            }
            catch { return new NeuroResponse(label, "[Agent failed to respond]"); }
        });

        var layerResults = (await Task.WhenAll(tasks)).ToList();

        if (synthesizer.Key != null)
        {
            var synthTopic = $"{topic}\n\nHere are the 3 biochemical agent responses:\n\n" +
                             string.Join("\n", layerResults.Select(r => $"[{r.Source}]: {r.Message}"));
            try
            {
                var response = await _llm.ChatWithProfileAsync(synthesizer.Value, [new(ChatRole.User, synthTopic)]);
                layerResults.Add(new NeuroResponse("Synthesizer", ResponseService.ExtractSuggestion(response) ?? response.Trim()));
            }
            catch { layerResults.Add(new NeuroResponse("Synthesizer", "[Synthesizer failed to respond]")); }
        }

        return layerResults;
    }

    private async Task<List<CustomAgent>?> CallLlmForAgentsAsync(string prompt, int agentCount)
    {
        try
        {
            var responseText = await _llm.AskAsync(prompt);
            var agents = ParseService.ParseJsonArray<List<CustomAgent>>(responseText);

            if (agents == null || agents.Count == 0)
                return null;

            return agents.Count > agentCount - 1 ? agents.Take(agentCount - 1).ToList() : agents;
        }
        catch
        {
            return null;
        }
    }
}
