namespace NeuroGateway.AgentFramework;

using Microsoft.Extensions.AI;
using NeuroGateway.Models;

public static class Orchestrator
{
    public static async Task<List<AgentResult>> RunAllAsync(
        ChatClient client,
        IReadOnlyList<AgentDefinition> agents,
        string userMessage,
        int maxConcurrency = 0,
        CancellationToken ct = default)
    {
        if (maxConcurrency <= 0)
        {
            var tasks = agents.Select(a => RunOneAsync(client, a, userMessage, ct));
            return [.. await Task.WhenAll(tasks)];
        }

        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var throttled = agents.Select(async a =>
        {
            await semaphore.WaitAsync(ct);
            try { return await RunOneAsync(client, a, userMessage, ct); }
            finally { semaphore.Release(); }
        });
        return [.. await Task.WhenAll(throttled)];
    }

    public static async Task<Dictionary<string, List<AgentResult>>> RunByLayerAsync(
        ChatClient client,
        IReadOnlyList<AgentDefinition> agents,
        string userMessage,
        int maxConcurrency = 0,
        CancellationToken ct = default)
    {
        var results = await RunAllAsync(client, agents, userMessage, maxConcurrency, ct);
        return results
            .GroupBy(r => r.Layer ?? "default")
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public static async Task<List<AgentResult>> RunSequentialAsync(
        ChatClient client,
        IReadOnlyList<AgentDefinition> agents,
        string userMessage,
        CancellationToken ct = default)
    {
        var results = new List<AgentResult>();
        var history = new List<ChatMessage> { new(ChatRole.User, userMessage) };

        foreach (var agent in agents)
        {
            var msgs = new List<ChatMessage>(history.Count + 1)
            {
                new(ChatRole.System, agent.SystemPrompt)
            };
            msgs.AddRange(history);

            try
            {
                var response = await client.SendAsync(msgs, ct);
                results.Add(new AgentResult(agent.Name, agent.Layer, response, Success: true));
                history.Add(new(ChatRole.Assistant, response));
            }
            catch (Exception ex)
            {
                results.Add(new AgentResult(agent.Name, agent.Layer, $"ERROR: {ex.Message}", Success: false));
            }
        }
        return results;
    }

    private static async Task<AgentResult> RunOneAsync(
        ChatClient client, AgentDefinition agent, string userMessage, CancellationToken ct)
    {
        try
        {
            var response = await client.SendAsync(agent.SystemPrompt, userMessage, ct);
            return new AgentResult(agent.Name, agent.Layer, response, Success: true);
        }
        catch (Exception ex)
        {
            return new AgentResult(agent.Name, agent.Layer, $"ERROR: {ex.Message}", Success: false);
        }
    }
}
