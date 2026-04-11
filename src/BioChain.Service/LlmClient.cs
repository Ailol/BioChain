using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BioChain.Agent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioChain.Service;

/// <summary>
/// Direct HTTP client for vLLM. Handles grammar-constrained generation
/// and tool-use chat completions.
/// </summary>
public sealed class LlmClient
{
    private readonly HttpClient _http;
    private readonly LlmOptions _opts;
    private readonly ILogger<LlmClient> _logger;

    public LlmClient(HttpClient http, IOptions<LlmOptions> opts, ILogger<LlmClient> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generate BNF via guided_grammar constrained decoding.
    /// </summary>
    public async Task<string> GenerateAsync(
        string systemPrompt, string userInput, string? grammar, CancellationToken ct)
    {
        var endpoint = GetEndpoint();
        var maxOutputTokens = EstimateOutputBudget(systemPrompt.Length + userInput.Length);

        _logger.LogInformation("Input ~{InputTokens} tokens, requesting {MaxTokens} output tokens",
            (systemPrompt.Length + userInput.Length) / 2 + 100, maxOutputTokens);

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _opts.Model,
            ["messages"] = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userInput }
            },
            ["max_tokens"] = maxOutputTokens,
            ["temperature"] = 0.3,
            ["top_p"] = 0.9,
            ["min_p"] = 0.05,
            ["presence_penalty"] = 0.5,
            ["chat_template_kwargs"] = new { enable_thinking = false }
        };

        if (grammar is not null)
        {
            requestBody["guided_grammar"] = grammar;
            _logger.LogInformation("Using guided_grammar ({GrammarLen} chars)", grammar.Length);
        }

        var jsonOpts = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var response = await _http.PostAsJsonAsync($"{endpoint}/chat/completions", requestBody, jsonOpts, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("LLM call failed ({Status}): {Body}",
                response.StatusCode, responseText[..Math.Min(500, responseText.Length)]);
            throw new InvalidOperationException($"LLM call failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseText);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return StripThinkingBlocks(content).Trim();
    }

    /// <summary>
    /// Chat completion with tool definitions. Returns raw content (may contain tool_call blocks).
    /// </summary>
    public async Task<string> ChatAsync(
        List<Dictionary<string, object>> messages, object[] toolDefinitions, CancellationToken ct)
    {
        var endpoint = GetEndpoint();

        var totalChars = messages.Sum(m =>
            m.TryGetValue("content", out var c) && c is string s ? s.Length : 0);
        var maxOutputTokens = EstimateOutputBudget(totalChars);

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _opts.Model,
            ["messages"] = messages,
            ["max_tokens"] = maxOutputTokens,
            ["temperature"] = 0.3,
            ["top_p"] = 0.9,
            ["min_p"] = 0.05,
            ["presence_penalty"] = 0.5,
            ["tools"] = toolDefinitions,
            ["tool_choice"] = "auto",
            ["chat_template_kwargs"] = new { enable_thinking = false }
        };

        var jsonOpts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var response = await _http.PostAsJsonAsync($"{endpoint}/chat/completions", requestBody, jsonOpts, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("LLM chat failed ({Status}): {Body}",
                response.StatusCode, responseText[..Math.Min(500, responseText.Length)]);
            throw new InvalidOperationException($"LLM call failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseText);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var msg = choice.GetProperty("message");

        string content = "";
        if (msg.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
            content = StripThinkingBlocks(contentEl.GetString() ?? "");

        // Convert structured tool_calls to Qwen-style text blocks
        if (msg.TryGetProperty("tool_calls", out var toolCallsEl) &&
            toolCallsEl.ValueKind == JsonValueKind.Array &&
            toolCallsEl.GetArrayLength() > 0)
        {
            var sb = new System.Text.StringBuilder(content);
            foreach (var tc in toolCallsEl.EnumerateArray())
            {
                var name = tc.GetProperty("function").GetProperty("name").GetString() ?? "";
                var argsStr = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                using var argsDoc = JsonDocument.Parse(argsStr);
                sb.AppendLine("<tool_call>");
                sb.AppendLine($"<function={name}>");
                foreach (var prop in argsDoc.RootElement.EnumerateObject())
                    sb.AppendLine($"<parameter={prop.Name}>{prop.Value}</parameter>");
                sb.AppendLine("</function>");
                sb.AppendLine("</tool_call>");
            }
            content = sb.ToString();
        }

        return content;
    }

    private int EstimateOutputBudget(int inputChars)
    {
        var estimatedInputTokens = inputChars / 2 + 100;
        var maxOutputTokens = Math.Min(8192, 32768 - estimatedInputTokens - 200);
        if (maxOutputTokens < 500)
        {
            _logger.LogWarning("Very limited output budget: {MaxTokens} tokens", maxOutputTokens);
            maxOutputTokens = 500;
        }
        return maxOutputTokens;
    }

    private string GetEndpoint()
    {
        var endpoint = _opts.Endpoint.TrimEnd('/');
        if (!endpoint.EndsWith("/v1"))
            endpoint += endpoint.EndsWith('/') ? "v1" : "/v1";
        return endpoint;
    }

    internal static string StripThinkingBlocks(string text)
    {
        while (true)
        {
            var start = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            var end = text.IndexOf("</think>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) { text = text[..start]; break; }
            text = text[..start] + text[(end + "</think>".Length)..];
        }
        return text;
    }
}
