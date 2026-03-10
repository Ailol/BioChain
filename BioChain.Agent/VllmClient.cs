using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BioChain.Agent;

/// <summary>
/// ILlmClient implementation using VLLM's OpenAI-compatible API.
/// Sends system prompt + user input → receives BNF text.
/// </summary>
public class VllmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public VllmClient(HttpClient http, string model)
    {
        _http = http;
        _model = model;
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userInput, string? existingBnf = null)
    {
        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt),
        };

        // If we have existing BNF context (e.g., for plasticity stage building on base),
        // include it as assistant context
        if (!string.IsNullOrEmpty(existingBnf))
        {
            messages.Add(new("assistant", $"Current program state:\n{existingBnf}"));
        }

        messages.Add(new("user", userInput));

        var request = new ChatCompletionRequest
        {
            Model = _model,
            Messages = messages,
            Temperature = 0.3f,
            MaxTokens = 8192,
        };

        var response = await _http.PostAsJsonAsync("/v1/chat/completions", request, _json);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(_json);
        return result?.Choices?.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("No response from VLLM");
    }
}

// ── OpenAI-compatible request/response types ─────────────────────────────────

file record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

file class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.3f;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 8192;
}

file class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }
}

file class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessageResponse? Message { get; set; }
}

file class ChatMessageResponse
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
