using NeuroGateway.Models;
using NeuroGateway.Utils;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Pure LLM orchestration steps: trait extraction and conversation analysis.
/// No repository or service dependencies — only LlmService + ParseService.
/// </summary>
public class Pipeline
{
    private readonly LlmService _llm;

    public Pipeline(LlmService llm)
    {
        _llm = llm;
    }

    /// <summary>
    /// Extract personality traits from document text via LLM.
    /// Returns ExtractedTrait objects (topic + explanation pairs) for caller to merge into content strings.
    /// </summary>
    public async Task<List<ExtractedTrait>> ExtractTraitsAsync(string text)
    {
        var prompt = "Analyze this document and extract personality and professional traits about the person. " +
                     "Return a JSON array of traits: [{\"topic\":\"...\",\"explanation\":\"...\"}]. " +
                     "Focus on: skills, work style, leadership patterns, communication style, values, interests, " +
                     "professional strengths, and behavioral tendencies. Only clear patterns. Empty [] if none.\n\n" + text;

        var resp = await _llm.AskAsync(prompt, _llm.OrchestratorModel);
        return ParseService.ParseExtractedTraits(resp);
    }

    /// <summary>
    /// Analyze conversation messages via LLM to find significant exchanges and extract traits.
    /// </summary>
    public async Task<List<ImportantConversation>> AnalyzeConversationsAsync(
        List<ConversationMessage> messages, string targetName, string userName)
    {
        if (messages.Count == 0) return [];

        var config = ConfigLoader.LoadJson<PromptConfig>("Prompts.json").ConversationAnalysis ?? new ConversationAnalysisConfig();
        var prompt = ParseService.BuildConversationAnalysisPrompt(messages, targetName, userName, config);
        var response = await _llm.AskAsync(prompt, _llm.OrchestratorModel);
        return ParseService.ParseImportantConversations(response, messages);
    }
}
