using Microsoft.Extensions.AI;
using Models;
using Repository;

namespace Agents;

/// <summary>
/// Service for biochemical analysis (3-layer SKIP/ADD) and data analysis (documents, conversations).
/// </summary>
public class AnalysisService
{
    private readonly LlmService _llm;
    private readonly PersonRepository _personRepo;
    private readonly EmbeddingRepository _embeddingRepo;
    private readonly EmbeddingService _embeddingService;
    private readonly PersonalityService _personalityService;
    private readonly PersonalityRepository _personalityRepo;
    private readonly RelationshipRepository _relationshipRepo;
    private readonly AgentTemplateRepository _templateRepo;
    private readonly AgentService _agentService;
    private readonly int _maxParallelAgents;

    private readonly Lazy<Task<Dictionary<string, AgentProfile>>> _neuroAgents;
    private readonly Lazy<Task<Dictionary<string, AgentProfile>>> _hormoneAgents;
    private readonly Lazy<Task<Dictionary<string, AgentProfile>>> _peptideAgents;
    private readonly Lazy<Task<Dictionary<string, ResponderGroupConfig>>> _neuroChatAgents;
    private readonly SuggestionConfig _prompts;

    public AnalysisService(LlmService llm, PersonRepository personRepo, EmbeddingRepository embeddingRepo,
        EmbeddingService embeddingService, PersonalityService personalityService,
        PersonalityRepository personalityRepo, RelationshipRepository relationshipRepo,
        AgentTemplateRepository templateRepo, AgentService agentService, AgentConfiguration config)
    {
        _llm = llm;
        _personRepo = personRepo;
        _embeddingRepo = embeddingRepo;
        _embeddingService = embeddingService;
        _personalityService = personalityService;
        _personalityRepo = personalityRepo;
        _relationshipRepo = relationshipRepo;
        _templateRepo = templateRepo;
        _agentService = agentService;
        _maxParallelAgents = config.MaxParallelAgents;

        _neuroAgents = new(() => _templateRepo.GetAnalyzingAgentsAsync("analyzing_neurotransmitter"));
        _hormoneAgents = new(() => _templateRepo.GetAnalyzingAgentsAsync("analyzing_hormone"));
        _peptideAgents = new(() => _templateRepo.GetAnalyzingAgentsAsync("analyzing_peptide"));
        _neuroChatAgents = new(() => _templateRepo.GetNeuroChatAgentsAsync());
        _prompts = ConfigLoader.LoadJson<PromptConfig>("Prompts.json").Suggestions ?? new SuggestionConfig();
    }

    // ===== Biochemical Analysis =====

    /// <summary>
    /// Run biochemical analysis. allLayers=true runs all 3 in parallel, false runs NT only.
    /// </summary>
    public async Task<(List<BiochemicalDecision> Nt, List<BiochemicalDecision> Hormone, List<BiochemicalDecision> Peptide)> RunAnalysisAsync(
        string person, string topic, string context, bool allLayers = true)
    {
        var neuroAgents = await _neuroAgents.Value;
        var ntTask = RunLayerAsync(person, topic, context, neuroAgents);

        if (!allLayers)
            return (await ntTask, [], []);

        var hormoneAgents = await _hormoneAgents.Value;
        var peptideAgents = await _peptideAgents.Value;
        var hormoneTask = RunLayerAsync(person, topic, context, hormoneAgents);
        var peptideTask = RunLayerAsync(person, topic, context, peptideAgents);
        await Task.WhenAll(ntTask, hormoneTask, peptideTask);
        return (ntTask.Result, hormoneTask.Result, peptideTask.Result);
    }

    private async Task<List<BiochemicalDecision>> RunLayerAsync(
        string person, string topic, string context, Dictionary<string, AgentProfile> agentProfiles)
    {
        var userMessage = $"Person: {person}\nTopic: {topic}\nContext: {context}";
        var allResults = new List<BiochemicalDecision>();

        foreach (var batch in agentProfiles.Chunk(_maxParallelAgents))
        {
            var tasks = batch.Select(async kv =>
            {
                var (name, profile) = kv;
                try
                {
                    var response = await _llm.ChatWithProfileAsync(profile, [new(ChatRole.User, userMessage)]);
                    var cleaned = response.TrimStart('*', ' ', '#');
                    if (cleaned.StartsWith("ADD:", StringComparison.OrdinalIgnoreCase))
                    {
                        var reasoning = cleaned[4..].Trim().TrimEnd('*');
                        return new BiochemicalDecision(name, reasoning);
                    }
                }
                catch { /* Skip agent on error */ }
                return null;
            });

            var results = await Task.WhenAll(tasks);
            allResults.AddRange(results.Where(d => d != null)!);
        }

        return allResults;
    }

    // ===== Neuroresponse Analysis =====

    /// <summary>
    /// Compute neurotransmitter weights and top matching traits for a person's message
    /// using vector similarity against stored trait embeddings.
    /// </summary>
    public async Task<NeuroresponseResult> GetNeuroresponseAsync(string person, string text)
    {
        var inputEmbedding = await _embeddingService.GenerateEmbeddingAsync(text);
        if (inputEmbedding == null)
            throw new InvalidOperationException("Failed to generate embedding for input text");

        var rawTraits = await _embeddingRepo.GetSimilarTraitsAsync(person, EmbeddingService.ToPostgresVector(inputEmbedding));

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
                kvp.Value.Count))
            .OrderByDescending(w => w.Weight)
            .ToList();

        var totalWeight = weights.Sum(w => w.Weight);
        if (totalWeight > 0)
            weights = weights.Select(w => new NeurotransmitterWeight(w.Neurotransmitter, w.Weight / totalWeight, w.TraitCount)).ToList();

        return new NeuroresponseResult(person, text, weights, topTraits);
    }

    // ===== Neurorespond Pipeline =====

    /// <summary>
    /// Full neurorespond pipeline: match person → neuroresponse analysis → profile build →
    /// NT analysis → narrative synthesis → 3+1 agent response generation.
    /// Uses deduplicated cluster-representative profiles for {chemicals} context.
    /// </summary>
    public async Task<NeuroNarrativeResult> NeuroRespondAsync(string person, string theirMessage, string? relationship)
    {
        var (matchedPerson, _) = await MatchPersonAsync(person, theirMessage);
        var neuroResult = await GetNeuroresponseAsync(matchedPerson, theirMessage);

        var fullScan = await _personalityService.GetFullPersonalityScanAsync(matchedPerson);
        var hormones = fullScan?.Hormones.Take(5).ToList() ?? [];
        var peptides = fullScan?.Peptides.Take(5).ToList() ?? [];

        var resolvedRelationship = await _relationshipRepo.EnsureRelationshipTypeAsync(relationship ?? "dating");
        var group = ParseService.ParseResponderGroup(relationship);

        var enhancedProfile = PersonalityService.BuildPersonProfile(neuroResult, fullScan, hormones, peptides);

        var context = $"""
            Message from {matchedPerson}: "{theirMessage}"
            Relationship: {resolvedRelationship}

            {enhancedProfile}
            """;

        // 1. Run SKIP/ADD analyzing agents (NT only for narrative synthesis)
        var (decisions, _, _) = await RunAnalysisAsync(matchedPerson, theirMessage, context, allLayers: false);

        // 2. Synthesize the analysis into a narrative
        var analysis = "";
        if (decisions.Count > 0)
        {
            var agentOutputs = string.Join("\n", decisions.Select(d => $"[{d.Chemical}]: {d.Reasoning}"));
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

            var synthResponse = await _llm.AskAsync(synthPrompt, _llm.ThinkingModel);
            analysis = ResponseService.ExtractConclusion(synthResponse) ?? synthResponse;
        }

        // 3. Generate 4 responses (1 NT + 1 hormone + 1 peptide + 1 synthesizer)
        // Use deduplicated profiles (cluster representatives) for richer {chemicals} context
        var deduped = await _personalityRepo.GetDeduplicatedProfilesAsync(matchedPerson);

        static string FormatProfile<T>(IList<T> items, Func<T, string> format, string fallback)
            => items.Count > 0 ? string.Join(", ", items.Select(format)) : fallback;

        var ntProfile = deduped.TryGetValue("neurotransmitter", out var ntReasonings) && ntReasonings.Count > 0
            ? string.Join("\n", ntReasonings)
            : FormatProfile(neuroResult.NeurotransmitterWeights, w => $"{w.Neurotransmitter} {w.Weight:P0}", "Dopamine (default)");
        var hormoneProfile = deduped.TryGetValue("hormone", out var hReasonings) && hReasonings.Count > 0
            ? string.Join("\n", hReasonings)
            : FormatProfile(hormones, h => $"{h.Name} ({h.TraitCount} traits)", "Cortisol (default)");
        var peptideProfile = deduped.TryGetValue("peptide", out var pReasonings) && pReasonings.Count > 0
            ? string.Join("\n", pReasonings)
            : FormatProfile(peptides, p => $"{p.Name} ({p.TraitCount} traits)", "Oxytocin (default)");

        var profileSection = $"WHO IS {matchedPerson.ToUpper()}:\n{enhancedProfile}";

        var topic = _prompts.TopicTemplate
            .Replace("{person}", matchedPerson)
            .Replace("{text}", theirMessage)
            .Replace("{group}", resolvedRelationship)
            .Replace("{profileSection}", profileSection)
            .Replace("{analysis}", analysis);

        var chatProfiles = await GetNeuroChatProfilesAsync(group);
        var responses = chatProfiles != null
            ? await _agentService.RunNeuroRespondAsync(chatProfiles, topic, ntProfile, hormoneProfile, peptideProfile)
            : [new NeuroResponse("Fallback", "No agent configuration found for this relationship group.")];

        return new NeuroNarrativeResult(matchedPerson, theirMessage, resolvedRelationship,
            new NeuroprofileData(neuroResult.NeurotransmitterWeights, neuroResult.TopMatchingTraits, hormones, peptides),
            decisions.ToDictionary(d => d.Chemical, d => d.Reasoning), analysis, responses);
    }

    private async Task<(string person, string matchedBy)> MatchPersonAsync(string personName, string message)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(message);
        var embeddingVector = embedding != null ? EmbeddingService.ToPostgresVector(embedding) : null;
        return await _personRepo.MatchPersonAsync(personName, embeddingVector);
    }

    private async Task<Dictionary<string, AgentProfile>?> GetNeuroChatProfilesAsync(ResponderGroup group)
    {
        var chatAgents = await _neuroChatAgents.Value;
        if (!chatAgents.TryGetValue(group.ToString(), out var groupConfig))
            return null;

        return groupConfig.Agents.ToDictionary(a => a.Name, a => new AgentProfile
        {
            Role = a.Role, Style = a.Style, MaxWords = a.MaxWords,
            Conclusion = a.IsSynthesizer, Layer = a.Layer
        });
    }

    // ===== Data Analysis =====

    public async Task<DocumentAnalysisResult> AnalyzeDataAsync(string text, string person, string documentType, bool embeddings = true)
    {
        var prompt = "Analyze this document and extract personality and professional traits about the person. " +
                     "Return a JSON array of traits: [{\"topic\":\"...\",\"explanation\":\"...\"}]. " +
                     "Focus on: skills, work style, leadership patterns, communication style, values, interests, " +
                     "professional strengths, and behavioral tendencies. Only clear patterns. Empty [] if none.\n\n" + text;

        var resp = await _llm.AskAsync(prompt, _llm.ThinkingModel);
        var extracted = ParseService.ParseTraits(resp);
        await _personRepo.CreatePersonAsync(person);

        var (added, decisions) = await AddExtractedTraitsAsync(person, extracted, embeddings);
        return new DocumentAnalysisResult(person, documentType, extracted, added, decisions);
    }

    public async Task<ConversationAnalysisResult> AnalyzeConversationAsync(ConversationAnalysisRequest request)
    {
        var format = request.FormatHint ?? ParseService.DetectConversationFormat(request.FileContent);
        var messages = ParseService.ParseConversation(request.FileContent, format,
            request.TargetPersonalityName, request.UserName);
        var parsed = await ParseConversationsAsync(messages, request.TargetPersonalityName, request.UserName);

        var allTraits = parsed.SelectMany(i => i.ExtractedTraits).ToList();
        var addedTraits = new List<Trait>();
        var neuroDecisions = new List<NeuroAgentDecision>();

        if (request.AutoAdd)
        {
            await _personRepo.CreatePersonAsync(request.TargetPersonalityName);
            var targetTraits = allTraits
                .Where(t => ParseService.IsSpeakerMatch(t.Speaker, request.TargetPersonalityName))
                .Select(t => new Trait(t.Topic, t.Explanation)).ToList();
            (addedTraits, neuroDecisions) = await AddExtractedTraitsAsync(request.TargetPersonalityName, targetTraits);
        }

        return new ConversationAnalysisResult(request.TargetPersonalityName, request.UserName,
            format, messages.Count, parsed.Count, parsed, allTraits, addedTraits, neuroDecisions);
    }

    private async Task<(List<Trait> Added, List<NeuroAgentDecision> Decisions)> AddExtractedTraitsAsync(
        string person, List<Trait> traits, bool embeddings = true)
    {
        var added = new List<Trait>();
        var decisions = new List<NeuroAgentDecision>();

        foreach (var t in traits)
        {
            var result = await _personalityService.AddPersonalityEntryAsync(person, t.Topic, t.Explanation, embeddings);
            added.AddRange(result.Added);
            decisions.AddRange(result.Added.Select(a =>
                new NeuroAgentDecision(a.Topic, a.Neurotransmitter ?? "Unknown", a.Explanation)));
        }

        return (added, decisions);
    }

    private async Task<List<ImportantConversation>> ParseConversationsAsync(
        List<ConversationMessage> messages, string targetName, string userName)
    {
        if (messages.Count == 0) return [];

        var config = ConfigLoader.LoadJson<PromptConfig>("Prompts.json").ConversationAnalysis ?? new ConversationAnalysisConfig();
        var prompt = ParseService.BuildConversationAnalysisPrompt(messages, targetName, userName, config);
        var response = await _llm.AskAsync(prompt, _llm.ThinkingModel);
        return ParseService.ParseImportantConversations(response, messages);
    }
}
