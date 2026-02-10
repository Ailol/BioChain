using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Utils;

namespace NeuroGateway.Service;

/// <summary>
/// Orchestrates biochemical analysis and data analysis (document/conversation).
/// Delegates LLM agent work to AgentFramework.Analyze and AgentFramework.Pipeline.
/// </summary>
public class AnalyseService
{
    private readonly Analyze _analyze;
    private readonly Pipeline _pipeline;
    private readonly PersonRepository _personRepo;
    private PersonalityService _personalityService;

    public AnalyseService(Analyze analyze, Pipeline pipeline, PersonRepository personRepo)
    {
        _analyze = analyze;
        _pipeline = pipeline;
        _personRepo = personRepo;
        _personalityService = null!;
    }

    /// <summary>
    /// Called by DI setup to break AnalyseService <-> PersonalityService circular dependency.
    /// </summary>
    public void SetPersonalityService(PersonalityService svc) => _personalityService = svc;

    /// <summary>
    /// Run biochemical analysis. allLayers=true runs all 3 in parallel, false runs NT only.
    /// </summary>
    public Task<(List<BiochemicalDecision> Nt, List<BiochemicalDecision> Hormone, List<BiochemicalDecision> Peptide)>
        RunAnalysisAsync(string person, string topic, string context, bool allLayers = true)
        => _analyze.RunAnalysisAsync(person, topic, context, allLayers);

    /// <summary>
    /// Analyze data (document text or conversation) and extract personality entries.
    /// </summary>
    public async Task<DataAnalysisResult> AnalyzeDataAsync(DataAnalysisRequest request)
    {
        List<ExtractedTrait> extracted;
        ConversationFormat? detectedFormat = null;
        int? totalMessages = null;
        int? importantCount = null;
        List<ImportantConversation>? exchanges = null;

        if (request.IsConversation)
        {
            var format = request.FormatHint ?? ParseService.DetectConversationFormat(request.Content);
            detectedFormat = format;
            var messages = ParseService.ParseConversation(request.Content, format,
                request.Person, request.UserName ?? "Ailo");
            totalMessages = messages.Count;
            var parsed = await _pipeline.AnalyzeConversationsAsync(messages, request.Person, request.UserName ?? "Ailo");
            importantCount = parsed.Count;
            exchanges = parsed;

            extracted = parsed.SelectMany(i => i.ExtractedTraits)
                .Where(t => ParseService.IsSpeakerMatch(t.Speaker, request.Person))
                .ToList();
        }
        else
        {
            var rawTraits = await _pipeline.ExtractTraitsAsync(request.Content);
            extracted = rawTraits.Select(t => new ExtractedTrait(t.Topic, t.Explanation, request.Person)).ToList();
        }

        var addedEntries = new List<AnalyzedEntry>();
        var neuroDecisions = new List<NeuroAgentDecision>();

        if (request.AutoAdd && extracted.Count > 0)
        {
            await _personRepo.CreatePersonAsync(request.Person);
            (addedEntries, neuroDecisions) = await AddExtractedEntriesAsync(
                request.Person, extracted, request.DocumentType ?? "document", request.Embeddings);
        }

        return new DataAnalysisResult(request.Person, request.DocumentType, detectedFormat,
            totalMessages, importantCount, exchanges, extracted, addedEntries, neuroDecisions);
    }

    private async Task<(List<AnalyzedEntry> Added, List<NeuroAgentDecision> Decisions)> AddExtractedEntriesAsync(
        string person, List<ExtractedTrait> traits, string sourceType, bool embeddings = true)
    {
        var added = new List<AnalyzedEntry>();
        var decisions = new List<NeuroAgentDecision>();

        foreach (var t in traits)
        {
            // Merge topic + explanation into single content string
            var content = $"{t.Topic}: {t.Explanation}";
            var result = await _personalityService.AnalyzeAsync(person, content, sourceType, embeddings: embeddings);
            added.AddRange(result.Added);
            decisions.AddRange(result.Added.Select(a =>
                new NeuroAgentDecision(a.Content, a.PrimaryNt, a.AllChemicals())));
        }

        return (added, decisions);
    }
}
