namespace NeuroGateway.AgentFramework;

using Microsoft.Extensions.AI;

/// <summary>
/// Thin LLM wrapper. Send system+user prompt, get text back.
/// One instance per LLM backend (injected via keyed DI).
/// Optionally applies default <see cref="ChatOptions"/> and assistant prefill to every call.
/// </summary>
public sealed class ChatClient(
    IChatClient inner,
    ChatOptions? defaultOptions = null,
    string? assistantPrefill = null)
{
    /// <summary>Send a system prompt + user message, get response text.
    /// If <paramref name="assistantPrefill"/> was set, it is appended as an assistant
    /// message (vLLM continues from it) and prepended to the returned text.</summary>
    public async Task<string> SendAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        if (assistantPrefill is not null)
            messages.Add(new(ChatRole.Assistant, assistantPrefill));

        var response = await inner.GetResponseAsync(messages, defaultOptions, cancellationToken: ct);
        var text = response.Text ?? "";
        return assistantPrefill is not null ? assistantPrefill + text : text;
    }

    /// <summary>Send a full message list, get response text.
    /// For multi-turn conversations (group chat, sequential agents).</summary>
    public async Task<string> SendAsync(
        IList<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var response = await inner.GetResponseAsync(messages, defaultOptions, cancellationToken: ct);
        return response.Text ?? "";
    }
}
