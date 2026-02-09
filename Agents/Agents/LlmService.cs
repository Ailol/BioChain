using Microsoft.Extensions.AI;
using Models;

namespace Agents;

/// <summary>
/// Centralized service for all LLM interactions (chat and embeddings).
/// Wraps IChatClient and IEmbeddingGenerator from Microsoft.Extensions.AI,
/// enabling Ollama, vLLM, or any OpenAI-compatible backend via DI configuration.
/// </summary>
public class LlmService
{
    private readonly IChatClient _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public string ThinkingModel { get; }
    public string InstructModel { get; }
    public LlmService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        AgentConfiguration config)
    {
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        ThinkingModel = config.ThinkingModel;
        InstructModel = config.InstructModel;
    }

    public async Task<string> ChatAsync(List<ChatMessage> messages, string? model = null)
    {
        var options = new ChatOptions { ModelId = model ?? InstructModel };
        var response = await _chatClient.GetResponseAsync(messages, options);
        return response.Text ?? "";
    }

    public Task<string> AskAsync(string prompt, string? model = null)
        => ChatAsync([new(ChatRole.User, prompt)], model);

    public async Task<string> ChatWithProfileAsync(AgentProfile profile, List<ChatMessage> history, string? model = null)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, profile.ToSystemPrompt()) };
        messages.AddRange(history);
        return await ChatAsync(messages, model);
    }

    public async Task<float[]?> EmbedAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var result = await _embeddingGenerator.GenerateAsync([text]);
            return result.FirstOrDefault()?.Vector.ToArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating embedding: {ex.Message}");
            return null;
        }
    }
}
