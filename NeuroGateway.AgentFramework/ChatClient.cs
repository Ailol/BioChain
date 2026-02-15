namespace NeuroGateway.AgentFramework;

using Microsoft.Extensions.AI;

/// <summary>
/// Thin LLM wrapper. Send system+user prompt, get text back.
/// One instance per LLM backend (injected via keyed DI).
/// </summary>
public sealed class ChatClient(IChatClient inner)
{
    /// <summary>Send a system prompt + user message, get response text.</summary>
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
        var response = await inner.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? "";
    }

    /// <summary>Send a full message list, get response text.
    /// For multi-turn conversations (group chat, sequential agents).</summary>
    public async Task<string> SendAsync(
        IList<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var response = await inner.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? "";
    }
}
