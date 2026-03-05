using Microsoft.Extensions.AI;

namespace BioChain.Kernel.Agents;

/// <summary>
/// Default LLM-based engine. Wraps <see cref="IChatClient"/> with system + user messages.
/// No domain-specific logic — callers build their own context strings.
/// </summary>
public sealed class LlmEngine(IChatClient client) : ILlmEngine
{
    public async Task<string> ProcessAsync(string systemPrompt, string userContext, CancellationToken ct = default)
    {
        var response = await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userContext),
        ], cancellationToken: ct);
        return response.Text ?? "";
    }
}
