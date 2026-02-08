using System.Text.Json;
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
    public string EmbeddingModel { get; }

    public LlmService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        AgentConfiguration config)
    {
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        ThinkingModel = config.ThinkingModel;
        InstructModel = config.InstructModel;
        EmbeddingModel = config.EmbeddingModel;
    }

    /// <summary>
    /// Send a chat request and return the response content string.
    /// </summary>
    public async Task<string> ChatAsync(List<ChatMessage> messages, string? model = null)
    {
        var options = new ChatOptions
        {
            ModelId = model ?? InstructModel
        };

        var response = await _chatClient.GetResponseAsync(messages, options);
        return response.Text ?? "";
    }

    /// <summary>
    /// Send a chat request with an AgentProfile system prompt prepended to the history.
    /// </summary>
    public async Task<string> ChatWithProfileAsync(AgentProfile profile, List<ChatMessage> history, string? model = null)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, profile.ToSystemPrompt())
        };
        messages.AddRange(history);
        return await ChatAsync(messages, model);
    }

    /// <summary>
    /// Generate an embedding vector for the given text.
    /// </summary>
    public async Task<float[]?> EmbedAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var result = await _embeddingGenerator.GenerateAsync([text]);
            var embedding = result.FirstOrDefault();
            return embedding?.Vector.ToArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating embedding: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse a JSON array from an LLM response string, handling markdown code blocks.
    /// </summary>
    public static T? ParseJsonArray<T>(string responseText) where T : class
    {
        var jsonStart = responseText.IndexOf('[');
        var jsonEnd = responseText.LastIndexOf(']') + 1;

        if (jsonStart < 0 || jsonEnd <= jsonStart)
            return null;

        var jsonStr = responseText[jsonStart..jsonEnd];
        return JsonSerializer.Deserialize<T>(jsonStr, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
