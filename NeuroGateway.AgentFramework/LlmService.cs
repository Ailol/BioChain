using Microsoft.Extensions.AI;
using NeuroGateway.Models;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Centralized service for all LLM interactions (chat and embeddings).
/// Routes chat requests to the correct provider based on model name:
///   OrchestratorModel → orchestrator client (VL model for MCP tool orchestrating, trait extraction, style gen)
///   AgentModel        → agent framework client (instruct model for biochem agents, neuroresponse layers, group chat)
/// Embedding always goes through its own dedicated provider.
/// </summary>
public class LlmService
{
    private readonly IChatClient _agentClient;
    private readonly IChatClient _orchestratorClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public string OrchestratorModel { get; }
    public string AgentModel { get; }

    public LlmService(
        IChatClient agentClient,
        IChatClient orchestratorClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        AgentConfiguration config)
    {
        _agentClient = agentClient;
        _orchestratorClient = orchestratorClient;
        _embeddingGenerator = embeddingGenerator;
        OrchestratorModel = config.Orchestrator!.Model;
        AgentModel = config.AgentFramework!.Model;
    }

    public async Task<string> ChatAsync(List<ChatMessage> messages, string? model = null)
    {
        var resolvedModel = model ?? AgentModel;
        var client = resolvedModel == OrchestratorModel ? _orchestratorClient : _agentClient;
        var options = new ChatOptions { ModelId = resolvedModel };
        var response = await client.GetResponseAsync(messages, options);
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

    public Task<float[]?> EmbedTraitAsync(string topic, string explanation)
        => EmbedAsync($"{topic}: {explanation}");
}
