using System.Text.Json;
using Microsoft.Extensions.AI;
using Models;
using Repository;

namespace Agents;

/// <summary>
/// Service for neuroresponse queries and agent-driven response/analysis flows.
/// </summary>
public class NeuroService
{
    private readonly PersonalityRepository _repo;
    private readonly EmbeddingService _embeddingService;
    private readonly LlmService _llm;
    private readonly SuggestionConfig _prompts;

    public NeuroService(EmbeddingService embeddingService, LlmService llm, PersonalityRepository repo)
    {
        _repo = repo;
        _embeddingService = embeddingService;
        _llm = llm;
        _prompts = LoadPrompts();
    }

    private static SuggestionConfig LoadPrompts()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "Prompts.json");
        if (!File.Exists(configPath))
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", "Prompts.json");

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<PromptConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return config?.Suggestions ?? new SuggestionConfig();
    }

    /// <summary>
    /// Query a person's personality using vector similarity to determine neurotransmitter weights.
    /// </summary>
    public async Task<NeuroresponseResult> GetNeuroresponseAsync(string person, string text)
    {
        var inputEmbedding = await _embeddingService.GenerateEmbeddingAsync(text);
        if (inputEmbedding == null)
            throw new InvalidOperationException("Failed to generate embedding for input text");

        var embeddingVector = EmbeddingService.ToPostgresVector(inputEmbedding);
        var rawTraits = await _repo.GetSimilarTraitsAsync(person, embeddingVector);

        var traitsByNeuro = new Dictionary<string, List<double>>();
        var topTraits = new List<MatchingTrait>();

        foreach (var (topic, _, neuro, similarity) in rawTraits)
        {
            if (!traitsByNeuro.ContainsKey(neuro))
                traitsByNeuro[neuro] = [];
            traitsByNeuro[neuro].Add(similarity);

            if (topTraits.Count < 5)
                topTraits.Add(new MatchingTrait(topic, neuro, similarity));
        }

        if (traitsByNeuro.Count == 0)
            throw new InvalidOperationException($"No traits with embeddings found for person '{person}'");

        var weights = traitsByNeuro
            .Select(kvp => new NeurotransmitterWeight(
                kvp.Key,
                kvp.Value.Average() * (1 + Math.Log(kvp.Value.Count)),
                kvp.Value.Count
            ))
            .OrderByDescending(w => w.Weight)
            .ToList();

        var totalWeight = weights.Sum(w => w.Weight);
        if (totalWeight > 0)
        {
            weights = weights
                .Select(w => new NeurotransmitterWeight(w.Neurotransmitter, w.Weight / totalWeight, w.TraitCount))
                .ToList();
        }

        return new NeuroresponseResult(person, text, weights, topTraits);
    }

    /// <summary>
    /// Neurorespond: full neuroprofile + SKIP/ADD analysis + synthesizer narrative + response suggestions.
    /// </summary>
    public async Task<NeuroNarrativeResult> NeuroAnalyzeAsync(
        string person,
        string theirMessage,
        string? relationship,
        GroupAgentService groupAgentService,
        PersonalityService personalityService,
        int suggestionCount = 3)
    {
        var (matchedPerson, _) = await MatchPersonAsync(person, theirMessage);
        var neuroResult = await GetNeuroresponseAsync(matchedPerson, theirMessage);

        var fullScan = await personalityService.GetFullPersonalityScanAsync(matchedPerson);
        var hormones = fullScan?.Hormones.Take(5).Select(h => new HormoneScore(h.Name, h.Strength)).ToList()
            ?? new List<HormoneScore>();
        var peptides = fullScan?.Peptides.Take(5).Select(p => new PeptideScore(p.Name, p.Strength)).ToList()
            ?? new List<PeptideScore>();

        var group = PersonalityService.ParseResponderGroup(relationship);
        var enhancedProfile = PersonalityService.BuildEnhancedPersonProfile(
            matchedPerson, neuroResult, fullScan, hormones, peptides);

        var context = $"""
            Message from {matchedPerson}: "{theirMessage}"
            Relationship: {group}

            {enhancedProfile}
            """;

        // 1. Run SKIP/ADD analyzing agents
        var decisions = await groupAgentService.RunNeuroAnalysisAsync(matchedPerson, theirMessage, context);
        var agents = decisions.ToDictionary(d => d.Neurotransmitter, d => d.Explanation);

        // 2. Synthesize the analysis into a narrative
        var agentOutputs = string.Join("\n", decisions.Select(d =>
            $"[{d.Neurotransmitter}]: {d.Explanation}"));

        var analysis = "";
        if (decisions.Count > 0)
        {
            var synthPrompt = $"""
                Person: {matchedPerson}
                Message: "{theirMessage}"
                Relationship: {group}

                Neurotransmitter agent analyses:
                {agentOutputs}

                Synthesize these into ONE cohesive psychological analysis of {matchedPerson} based on this message.
                Write in the language the person wrote in. Be concise but insightful.
                End with CONCLUSION: followed by the final analysis.
                """;

            var synthMessages = new List<ChatMessage> { new(ChatRole.User, synthPrompt) };
            var synthResponse = await _llm.ChatAsync(synthMessages, _llm.ThinkingModel);
            analysis = ResponseService.ExtractConclusion(synthResponse) ?? synthResponse;
        }

        // 3. Generate response suggestions via NeuroChatAgents (if requested)
        var suggestions = new List<string>();
        if (suggestionCount > 0)
        {
            var personProfile = PersonalityService.BuildPersonProfile(matchedPerson, neuroResult, hormones, peptides);
            var profileSection = string.IsNullOrWhiteSpace(personProfile)
                ? $"{matchedPerson}'s neuroprofile: {string.Join(", ", neuroResult.NeurotransmitterWeights.Take(3).Select(w => $"{w.Neurotransmitter}: {w.Weight:P0}"))}"
                : $"WHO IS {matchedPerson.ToUpper()}:\n{personProfile}";

            var topic = _prompts.TopicTemplate
                .Replace("{person}", matchedPerson)
                .Replace("{text}", theirMessage)
                .Replace("{group}", group.ToString())
                .Replace("{profileSection}", profileSection)
                .Replace("{analysis}", analysis);

            var synthInstruction = _prompts.SynthesizerTemplate
                .Replace("{count}", suggestionCount.ToString())
                .Replace("{person}", matchedPerson);

            var chatProfiles = groupAgentService.GetNeuroChatProfiles(group);
            var (fullOutput, _) = chatProfiles != null
                ? await groupAgentService.RunGroupChatAsync(chatProfiles, topic, synthInstruction)
                : ("No agent configuration found for group", null);
            suggestions = ResponseService.ExtractAllSuggestions(fullOutput, suggestionCount);

            if (suggestions.Count == 0)
            {
                var fallback = ResponseService.ExtractCraftedResponse(fullOutput);
                if (fallback != null) suggestions.Add(fallback);
            }
        }

        var neuroprofile = new NeuroprofileData(
            neuroResult.NeurotransmitterWeights,
            neuroResult.TopMatchingTraits,
            hormones,
            peptides);

        return new NeuroNarrativeResult(matchedPerson, theirMessage, group,
            neuroprofile, agents, analysis, suggestions);
    }

    private async Task<(string person, string matchedBy)> MatchPersonAsync(string personName, string message)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(message);
        var embeddingVector = embedding != null ? EmbeddingService.ToPostgresVector(embedding) : null;
        return await _repo.MatchPersonAsync(personName, embeddingVector);
    }
}
