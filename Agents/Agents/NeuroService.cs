using Microsoft.Extensions.AI;
using Models;
using Repository;

namespace Agents;

/// <summary>
/// Service for neuroresponse queries and agent-driven response/analysis flows.
/// </summary>
public class NeuroService
{
    private readonly PersonRepository _personRepo;
    private readonly EmbeddingRepository _embeddingRepo;
    private readonly RelationshipRepository _relationshipRepo;
    private readonly EmbeddingService _embeddingService;
    private readonly LlmService _llm;
    private readonly SuggestionConfig _prompts;

    public NeuroService(EmbeddingService embeddingService, LlmService llm, PersonRepository personRepo,
        EmbeddingRepository embeddingRepo, RelationshipRepository relationshipRepo)
    {
        _personRepo = personRepo;
        _embeddingRepo = embeddingRepo;
        _relationshipRepo = relationshipRepo;
        _embeddingService = embeddingService;
        _llm = llm;
        _prompts = ConfigLoader.LoadJson<PromptConfig>("Prompts.json").Suggestions ?? new SuggestionConfig();
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
        var rawTraits = await _embeddingRepo.GetSimilarTraitsAsync(person, embeddingVector);

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
    /// Neurorespond: full neuroprofile + SKIP/ADD analysis + narrative + 4 biochemical responses (NT, hormone, peptide, synthesizer).
    /// </summary>
    public async Task<NeuroNarrativeResult> NeuroAnalyzeAsync(
        string person,
        string theirMessage,
        string? relationship,
        GroupAgentService groupAgentService,
        PersonalityService personalityService)
    {
        var (matchedPerson, _) = await MatchPersonAsync(person, theirMessage);
        var neuroResult = await GetNeuroresponseAsync(matchedPerson, theirMessage);

        var fullScan = await personalityService.GetFullPersonalityScanAsync(matchedPerson);
        var hormones = fullScan?.Hormones.Take(5).Select(h => new HormoneScore(h.Name, h.TraitCount)).ToList()
            ?? new List<HormoneScore>();
        var peptides = fullScan?.Peptides.Take(5).Select(p => new PeptideScore(p.Name, p.TraitCount)).ToList()
            ?? new List<PeptideScore>();

        // Resolve relationship: auto-create if not in DB, map to closest ResponderGroup
        var resolvedRelationship = await _relationshipRepo.EnsureRelationshipTypeAsync(relationship ?? "dating");
        var group = PersonalityService.ParseResponderGroup(relationship);

        var enhancedProfile = PersonalityService.BuildEnhancedPersonProfile(
            matchedPerson, neuroResult, fullScan, hormones, peptides);

        var context = $"""
            Message from {matchedPerson}: "{theirMessage}"
            Relationship: {resolvedRelationship}

            {enhancedProfile}
            """;

        // 1. Run SKIP/ADD analyzing agents (all 3 layers)
        var decisions = await groupAgentService.RunNeuroAnalysisAsync(matchedPerson, theirMessage, context);
        var agents = decisions.ToDictionary(d => d.Chemical, d => d.Reasoning);

        // 2. Synthesize the analysis into a narrative
        var agentOutputs = string.Join("\n", decisions.Select(d =>
            $"[{d.Chemical}]: {d.Reasoning}"));

        var analysis = "";
        if (decisions.Count > 0)
        {
            var synthPrompt = $"""
                Person: {matchedPerson}
                Message: "{theirMessage}"
                Relationship: {resolvedRelationship}

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

        // 3. Generate 4 responses via RunNeuroRespondAsync (1 NT + 1 hormone + 1 peptide + 1 synthesizer)
        // Pass full chemical profiles per layer so agents synthesize across ALL chemicals, not just top-1
        var ntProfile = neuroResult.NeurotransmitterWeights.Count > 0
            ? string.Join(", ", neuroResult.NeurotransmitterWeights.Select(w => $"{w.Neurotransmitter} {w.Weight:P0}"))
            : "Dopamine (default)";
        var hormoneProfile = hormones.Count > 0
            ? string.Join(", ", hormones.Select(h => $"{h.Name} ({h.TraitCount} traits)"))
            : "Cortisol (default)";
        var peptideProfile = peptides.Count > 0
            ? string.Join(", ", peptides.Select(p => $"{p.Name} ({p.TraitCount} traits)"))
            : "Oxytocin (default)";

        var personProfile = PersonalityService.BuildPersonProfile(matchedPerson, neuroResult, hormones, peptides);
        var profileSection = string.IsNullOrWhiteSpace(personProfile)
            ? $"{matchedPerson}'s neuroprofile: {string.Join(", ", neuroResult.NeurotransmitterWeights.Take(3).Select(w => $"{w.Neurotransmitter}: {w.Weight:P0}"))}"
            : $"WHO IS {matchedPerson.ToUpper()}:\n{personProfile}";

        var topic = _prompts.TopicTemplate
            .Replace("{person}", matchedPerson)
            .Replace("{text}", theirMessage)
            .Replace("{group}", resolvedRelationship)
            .Replace("{profileSection}", profileSection)
            .Replace("{analysis}", analysis);

        var chatProfiles = groupAgentService.GetNeuroChatProfiles(group);
        var responses = chatProfiles != null
            ? await groupAgentService.RunNeuroRespondAsync(chatProfiles, topic, ntProfile, hormoneProfile, peptideProfile)
            : new List<NeuroResponse> { new("Fallback", "No agent configuration found for this relationship group.") };

        var neuroprofile = new NeuroprofileData(
            neuroResult.NeurotransmitterWeights,
            neuroResult.TopMatchingTraits,
            hormones,
            peptides);

        return new NeuroNarrativeResult(matchedPerson, theirMessage, resolvedRelationship,
            neuroprofile, agents, analysis, responses);
    }

    private async Task<(string person, string matchedBy)> MatchPersonAsync(string personName, string message)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(message);
        var embeddingVector = embedding != null ? EmbeddingService.ToPostgresVector(embedding) : null;
        return await _personRepo.MatchPersonAsync(personName, embeddingVector);
    }
}
