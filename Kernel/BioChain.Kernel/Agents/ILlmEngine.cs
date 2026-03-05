namespace BioChain.Kernel.Agents;

/// <summary>
/// Generic LLM engine. Callers provide system prompt + user context,
/// engine handles the LLM call. Swappable: LLM-based, rule-based, mock, etc.
/// </summary>
public interface ILlmEngine
{
    Task<string> ProcessAsync(string systemPrompt, string userContext, CancellationToken ct = default);
}
