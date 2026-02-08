using System.Text.Json;
using Microsoft.Extensions.AI;
using Models;
using Repository;

namespace Agents;

public class PersonalityService
{
    private readonly LlmService _llm;
    private readonly PersonalityRepository _repo;
    private readonly GroupAgentService _groupAgentService;
    private readonly EmbeddingService _embeddingService;
    private readonly VectorService _vectorService;
    private readonly ConversationAnalysisConfig _conversationConfig;

    public PersonalityService(LlmService llm, GroupAgentService groupAgentService, EmbeddingService embeddingService, VectorService vectorService, PersonalityRepository repo)
    {
        _llm = llm;
        _repo = repo;
        _groupAgentService = groupAgentService;
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _conversationConfig = LoadConversationConfig();
    }

    private static ConversationAnalysisConfig LoadConversationConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "Prompts.json");
        if (!File.Exists(configPath))
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", "Prompts.json");

        if (!File.Exists(configPath))
            return new ConversationAnalysisConfig();

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<PromptConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return config?.ConversationAnalysis ?? new ConversationAnalysisConfig();
    }

    public async Task<FullPersonalityScan?> GetFullPersonalityScanAsync(string person)
    {
        var personalityResult = await _repo.GetPersonalityAsync(person);
        if (personalityResult.Profile == null) return null;

        var traits = personalityResult.Profile.Traits;
        var matchedName = personalityResult.Profile.Person;

        var traitsWithEmbeddings = await _repo.GetTraitEmbeddingsWithMetadataAsync(person);

        if (traitsWithEmbeddings.Count == 0)
            return new FullPersonalityScan(matchedName, traits, [], []);

        var rawEmbeddings = traitsWithEmbeddings.Select(t => t.Embedding).ToList();
        var hormones = await _repo.ComputeVectorScoresAsync("hormone", rawEmbeddings);
        var peptides = await _repo.ComputeVectorScoresAsync("peptide", rawEmbeddings);

        // Vector analysis
        var clusters = _vectorService.ClusterTraits(traitsWithEmbeddings);
        var neighbors = _vectorService.FindTraitNeighbors(traitsWithEmbeddings);
        var centroids = _vectorService.ComputeNtCentroids(traitsWithEmbeddings);
        var heatmap = await _vectorService.ComputeHeatmapAsync(traitsWithEmbeddings, "hormone");

        return new FullPersonalityScan(matchedName, traits, hormones, peptides, clusters, neighbors, centroids, heatmap);
    }

    public async Task<NeuroGroupResult> UpdatePersonalityAsync(string person, string topic, string context, bool embeddings = true, bool useCVAgents = false)
    {
        var decisions = useCVAgents
            ? await _groupAgentService.RunNeuroCVAnalysisAsync(person, topic, context)
            : await _groupAgentService.RunNeuroAnalysisAsync(person, topic, context);

        if (decisions.Count == 0)
            return new NeuroGroupResult(person, topic, [], "No neurotransmitters found this relevant.");

        await _repo.EnsurePersonExistsAsync(person);

        var added = new List<Trait>();
        foreach (var decision in decisions)
        {
            // Upsert trait immediately without embedding
            await _repo.UpsertPersonalityTraitAsync(person, decision.Neurotransmitter, topic, decision.Explanation, null);
            added.Add(new Trait(topic, decision.Explanation, decision.Neurotransmitter));
        }

        if (embeddings)
        {
            // Fire-and-forget: generate embeddings in background
            _ = Task.Run(async () =>
            {
                foreach (var decision in decisions)
                {
                    try
                    {
                        var embedding = await _embeddingService.GenerateTraitEmbeddingAsync(topic, decision.Explanation);
                        if (embedding != null)
                        {
                            var embeddingValue = EmbeddingService.ToPostgresVector(embedding);
                            await _repo.UpdateTraitEmbeddingByContentAsync(person, decision.Neurotransmitter, topic, embeddingValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Background embedding failed for {person}/{decision.Neurotransmitter}: {ex.Message}");
                    }
                }
            });
        }

        var msg = embeddings
            ? $"{added.Count} neurotransmitter(s) added entries. Embeddings generating in background."
            : $"{added.Count} neurotransmitter(s) added entries. Embeddings skipped.";
        return new NeuroGroupResult(person, topic, added, msg);
    }

    public async Task<ScanResult> ScanChatAsync(string person, List<ChatMessage> chat, bool autoAdd = false)
    {
        var text = string.Join("\n", chat.Select(m => $"{m.Role.ToString().ToUpper()}: {m.Text}"));
        var prompt = "Extract behavior traits as JSON: [{\"topic\":\"...\",\"explanation\":\"...\"}]. Only clear patterns. Empty [] if none.\n\n" + text;

        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var resp = await _llm.ChatAsync(messages, _llm.ThinkingModel);
        var extracted = ParseService.ParseTraits(resp);
        var added = new List<Trait>();

        if (autoAdd)
            foreach (var t in extracted)
            {
                var result = await UpdatePersonalityAsync(person, t.Topic, t.Explanation);
                added.AddRange(result.Added);
            }

        return new ScanResult(person, extracted, added);
    }

    // ===== Document Analysis Methods =====

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(string text, string person, string documentType, bool embeddings = true, bool useCVAgents = false)
    {
        var prompt = "Analyze this document and extract personality and professional traits about the person. " +
                     "Return a JSON array of traits: [{\"topic\":\"...\",\"explanation\":\"...\"}]. " +
                     "Focus on: skills, work style, leadership patterns, communication style, values, interests, " +
                     "professional strengths, and behavioral tendencies. Only clear patterns. Empty [] if none.\n\n" + text;

        var llmMessages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var resp = await _llm.ChatAsync(llmMessages, _llm.ThinkingModel);
        var extracted = ParseService.ParseTraits(resp);

        await _repo.CreatePersonAsync(person);

        var added = new List<Trait>();
        var neuroDecisions = new List<NeuroAgentDecision>();

        foreach (var t in extracted)
        {
            var result = await UpdatePersonalityAsync(person, t.Topic, t.Explanation, embeddings, useCVAgents);
            added.AddRange(result.Added);
            neuroDecisions.AddRange(result.Added.Select(a =>
                new NeuroAgentDecision(a.Topic, a.Neurotransmitter!, a.Explanation)));
        }

        return new DocumentAnalysisResult(person, documentType, extracted, added, neuroDecisions);
    }

    // ===== Conversation Analysis Methods =====

    public async Task<ConversationAnalysisResult> AnalyzeConversationAsync(ConversationAnalysisRequest request)
    {
        var format = request.FormatHint ?? ParseService.DetectConversationFormat(request.FileContent);

        var messages = ParseService.ParseConversation(request.FileContent, format,
            request.TargetPersonalityName, request.UserName);

        var important = await ExtractImportantConversationsAsync(messages,
            request.TargetPersonalityName, request.UserName);

        var allTraits = important.SelectMany(i => i.ExtractedTraits).ToList();

        var addedTraits = new List<Trait>();
        var neuroDecisions = new List<NeuroAgentDecision>();

        if (request.AutoAdd)
        {
            await _repo.CreatePersonAsync(request.TargetPersonalityName);

            foreach (var trait in allTraits.Where(t =>
                ParseService.IsSpeakerMatch(t.Speaker, request.TargetPersonalityName)))
            {
                var result = await UpdatePersonalityAsync(
                    request.TargetPersonalityName, trait.Topic, trait.Explanation);

                addedTraits.AddRange(result.Added);
                neuroDecisions.AddRange(result.Added.Select(a =>
                    new NeuroAgentDecision(a.Topic, a.Neurotransmitter!, a.Explanation)));
            }
        }

        return new ConversationAnalysisResult(
            request.TargetPersonalityName,
            request.UserName,
            format,
            messages.Count,
            important.Count,
            important,
            allTraits,
            addedTraits,
            neuroDecisions
        );
    }

    private async Task<List<ImportantConversation>> ExtractImportantConversationsAsync(
        List<ConversationMessage> messages,
        string targetName,
        string userName)
    {
        if (messages.Count == 0)
            return [];

        var conversationText = string.Join("\n", messages.Select((m, i) =>
            $"[{i}] {(m.IsTargetPersonality ? targetName : userName)}: {m.Content}"));

        var jsonExample = JsonSerializer.Serialize(_conversationConfig.JsonExample,
            new JsonSerializerOptions { WriteIndented = true })
            .Replace("{targetName}", targetName);

        var prompt = _conversationConfig.PromptTemplate
            .Replace("{targetName}", targetName)
            .Replace("{userName}", userName)
            .Replace("{conversationText}", conversationText)
            .Replace("{jsonExample}", jsonExample);

        var llmMessages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var response = await _llm.ChatAsync(llmMessages, _llm.ThinkingModel);
        return ParseService.ParseImportantConversations(response, messages);
    }

    public static Dictionary<string, AgentProfile> ToAgentProfiles(List<CustomAgent> agents)
    {
        var profiles = new Dictionary<string, AgentProfile>();
        foreach (var agent in agents)
        {
            profiles[agent.Name] = new AgentProfile
            {
                Role = agent.Role,
                Responsibilities = agent.Responsibilities,
                Style = agent.Style,
                MaxWords = agent.MaxWords,
                Conclusion = agent.IsSynthesizer
            };
        }
        return profiles;
    }

    public static ResponderGroup ParseResponderGroup(string? relationship)
    {
        if (!string.IsNullOrWhiteSpace(relationship) &&
            Enum.TryParse<ResponderGroup>(relationship, true, out var parsedGroup))
            return parsedGroup;
        return ResponderGroup.Dating;
    }

    /// <summary>
    /// Build a concise personality profile string for suggestion agents.
    /// Raw data only — no hardcoded NT descriptions.
    /// </summary>
    public static string BuildPersonProfile(
        string person,
        NeuroresponseResult neuroResult,
        List<HormoneScore>? hormones,
        List<PeptideScore>? peptides)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Neurochemistry: {string.Join(", ", neuroResult.NeurotransmitterWeights.Take(3).Select(w => $"{w.Neurotransmitter} {w.Weight:P0}"))}");

        var traits = neuroResult.TopMatchingTraits.Take(5).ToList();
        if (traits.Count > 0)
            sb.AppendLine($"Active traits: {string.Join(", ", traits.Select(t => $"{t.Topic} ({t.Neurotransmitter}, {t.Similarity:P0})"))}");

        if (hormones?.Count > 0)
            sb.AppendLine($"Hormones: {string.Join(", ", hormones.Take(3).Select(h => $"{h.Name} ({h.Strength:P0})"))}");

        if (peptides?.Count > 0)
            sb.AppendLine($"Peptides: {string.Join(", ", peptides.Take(3).Select(p => $"{p.Name} ({p.Strength:P0})"))}");

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Build a detailed personality profile for analysis agents.
    /// Raw data only — agents interpret the neurotransmitter meanings themselves.
    /// </summary>
    public static string BuildEnhancedPersonProfile(
        string person,
        NeuroresponseResult neuroResult,
        FullPersonalityScan? fullScan,
        List<HormoneScore> hormones,
        List<PeptideScore> peptides)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("NEUROTRANSMITTER WEIGHTS:");
        foreach (var w in neuroResult.NeurotransmitterWeights)
            sb.AppendLine($"  {w.Neurotransmitter}: {w.Weight:P0} ({w.TraitCount} traits)");

        if (fullScan?.Traits.Count > 0)
        {
            sb.AppendLine("\nPERSONALITY TRAITS:");
            foreach (var group in fullScan.Traits.GroupBy(t => t.Neurotransmitter ?? "Unknown"))
            {
                sb.AppendLine($"  [{group.Key}]:");
                foreach (var trait in group)
                    sb.AppendLine($"    - {trait.Topic}: {trait.Explanation}");
            }
        }

        if (neuroResult.TopMatchingTraits.Count > 0)
        {
            sb.AppendLine("\nMOST RELEVANT TRAITS FOR THIS MESSAGE:");
            foreach (var t in neuroResult.TopMatchingTraits)
                sb.AppendLine($"  - {t.Topic} ({t.Neurotransmitter}, {t.Similarity:P0})");
        }

        if (hormones.Count > 0)
            sb.AppendLine($"\nHORMONES: {string.Join(", ", hormones.Select(h => $"{h.Name} ({h.Strength:P0})"))}");

        if (peptides.Count > 0)
            sb.AppendLine($"\nPEPTIDES: {string.Join(", ", peptides.Select(p => $"{p.Name} ({p.Strength:P0})"))}");

        return sb.ToString().Trim();
    }
}
