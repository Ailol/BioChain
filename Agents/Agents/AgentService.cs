using Microsoft.Extensions.AI;
using Models;

namespace Agents;

/// <summary>
/// Service for generating custom agent teams and analyzing text for personality traits.
/// </summary>
public class AgentService
{
    private readonly LlmService _llm;
    private readonly PromptConfig _prompts;
    private readonly string _personalityAgentPrompt;
    private readonly string _roleAgentPrompt;
    private readonly string _traitAnalysisPrompt;

    public AgentService(LlmService llm)
    {
        _llm = llm;
        _prompts = ConfigLoader.LoadJson<PromptConfig>("Prompts.json");
        _personalityAgentPrompt = ConfigLoader.LoadPromptText("PersonalityAgentGeneration.txt");
        _roleAgentPrompt = ConfigLoader.LoadPromptText("RoleAgentGeneration.txt");
        _traitAnalysisPrompt = ConfigLoader.LoadPromptText("TraitAnalysis.txt");
    }

    /// <summary>
    /// Generate agents from a personality profile using LLM.
    /// </summary>
    public async Task<List<CustomAgent>?> GenerateAgentsFromPersonalityAsync(PersonalityProfile profile, int agentCount)
    {
        var traitsDescription = profile.Traits.Count > 0
            ? string.Join("\n", profile.Traits.Select(t =>
                $"- {t.Topic}: {t.Explanation} (linked to {t.Neurotransmitter ?? "general"})"))
            : "No specific traits recorded yet - generate agents based on a general balanced personality.";

        var prompt = _personalityAgentPrompt
            .Replace("{agentCount}", (agentCount - 1).ToString())
            .Replace("{person}", profile.Person)
            .Replace("{traitsDescription}", traitsDescription)
            .Replace("{agentJsonExample}", _prompts.AgentJsonExample);

        var agents = await CallLlmForAgentsAsync(prompt, agentCount);
        if (agents == null) return null;

        agents.Add(CreateSynthesizer($"{profile.Person}'s Integrator", [
            "Integrate all agent perspectives into a cohesive understanding",
            $"Relate conclusions back to {profile.Person}'s personality traits",
            "Identify common themes and actionable insights",
            "Provide an executive summary honoring all viewpoints"
        ], "integrative, personality-aware, draws meaningful conclusions. End with CONCLUSION: followed by key insights."));

        return agents;
    }

    /// <summary>
    /// Generate agents from a professional role/archetype using LLM.
    /// </summary>
    public async Task<List<CustomAgent>?> GenerateAgentsFromRoleAsync(string role, int agentCount)
    {
        var roleExamples = string.Join("\n", _prompts.RoleExamples.Select(kv =>
            $"            - \"{kv.Key}\": {string.Join(", ", kv.Value)}"));

        var prompt = _roleAgentPrompt
            .Replace("{agentCount}", (agentCount - 1).ToString())
            .Replace("{role}", role)
            .Replace("{agentJsonExample}", _prompts.AgentJsonExample)
            .Replace("{roleExamples}", roleExamples);

        var agents = await CallLlmForAgentsAsync(prompt, agentCount);
        if (agents == null) return null;

        agents.Add(CreateSynthesizer($"{role} Team Lead", [
            "Integrate all team perspectives into actionable recommendations",
            "Identify consensus and highlight key disagreements",
            "Prioritize insights based on impact and feasibility",
            "Provide an executive summary with clear next steps"
        ], $"decisive, experienced {role} leader who synthesizes team input. End with CONCLUSION: followed by key decisions and action items."));

        return agents;
    }

    /// <summary>
    /// Analyze raw text to extract personality traits using LLM.
    /// </summary>
    public async Task<List<AnalyzedTrait>> AnalyzeTextForTraitsAsync(string rawText)
    {
        var ntGuide = string.Join("\n", _prompts.NeurotransmitterGuide.Select(kv =>
            $"            - {kv.Key}: {kv.Value}"));

        var prompt = _traitAnalysisPrompt
            .Replace("{ntGuide}", ntGuide)
            .Replace("{rawText}", rawText)
            .Replace("{traitJsonExample}", _prompts.TraitJsonExample);

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt)
            };

            var responseText = await _llm.ChatAsync(messages);
            return LlmService.ParseJsonArray<List<AnalyzedTrait>>(responseText) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<CustomAgent>?> CallLlmForAgentsAsync(string prompt, int agentCount)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt)
            };

            var responseText = await _llm.ChatAsync(messages);
            var agents = LlmService.ParseJsonArray<List<CustomAgent>>(responseText);

            if (agents == null || agents.Count == 0)
                return null;

            if (agents.Count > agentCount - 1)
                agents = agents.Take(agentCount - 1).ToList();

            return agents;
        }
        catch
        {
            return null;
        }
    }

    private static CustomAgent CreateSynthesizer(string role, List<string> responsibilities, string style) =>
        new("Synthesizer", role, responsibilities, style, 300, true);
}
