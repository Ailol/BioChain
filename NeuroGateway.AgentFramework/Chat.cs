using Microsoft.Extensions.AI;
using NeuroGateway.Models;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Group chat orchestration: parallel (with synthesizer) and sequential modes.
/// </summary>
public class Chat
{
    private readonly LlmService _llm;

    public Chat(LlmService llm)
    {
        _llm = llm;
    }

    /// <summary>
    /// Run a group chat. When synthesizerInstruction is provided, agents run in parallel
    /// and the synthesizer combines their outputs. Without it, agents run sequentially
    /// with shared conversation history.
    /// </summary>
    public async Task<(string FullOutput, string? SynthesizerOutput)> RunGroupChatAsync(
        Dictionary<string, AgentProfile> profiles,
        string topic,
        string? synthesizerInstruction = null,
        int maxIterations = 8)
    {
        var synthesizer = synthesizerInstruction != null
            ? profiles.FirstOrDefault(p => p.Value.Conclusion)
            : default;

        var chatAgents = synthesizer.Key != null
            ? profiles.Where(p => !p.Value.Conclusion).ToDictionary(p => p.Key, p => p.Value)
            : profiles;

        List<(string Name, string Response)> agentOutputs;

        if (synthesizer.Key != null)
        {
            var tasks = chatAgents.Select(async kv =>
            {
                try
                {
                    var response = await _llm.ChatWithProfileAsync(kv.Value, [new(ChatRole.User, topic)]);
                    return (Name: kv.Key, Response: response);
                }
                catch { return (Name: kv.Key, Response: (string?)null); }
            });

            var results = await Task.WhenAll(tasks);
            agentOutputs = results.Where(r => r.Response != null).Select(r => (r.Name, r.Response!)).ToList();
        }
        else
        {
            agentOutputs = [];
            var conversationHistory = new List<ChatMessage> { new(ChatRole.User, topic) };
            var agents = chatAgents.Keys.ToArray();

            for (int i = 0; i < maxIterations && i < agents.Length; i++)
            {
                var name = agents[i % agents.Length];
                var profile = chatAgents[name];

                try
                {
                    var response = await _llm.ChatWithProfileAsync(profile, conversationHistory);
                    agentOutputs.Add((name, response));
                    conversationHistory.Add(new ChatMessage(ChatRole.Assistant, $"[{name}]: {response}"));

                    if (profile.Conclusion && response.Contains("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                        break;
                }
                catch { /* Skip agent on error */ }
            }
        }

        string? synthesizerOutput = null;
        if (synthesizer.Key != null && agentOutputs.Count > 0)
        {
            var synthTopic = $"{topic}\n\nHere are the agent responses:\n\n" +
                             string.Join("\n", agentOutputs.Select(a => $"[{a.Name}]: {a.Response}")) +
                             $"\n\n{synthesizerInstruction}";
            try
            {
                synthesizerOutput = await _llm.ChatWithProfileAsync(synthesizer.Value, [new(ChatRole.User, synthTopic)]);
            }
            catch { /* Synthesizer failed */ }
        }

        var fullOutput = string.Join("\n", agentOutputs.Select(a => a.Response));
        if (synthesizerOutput != null)
            fullOutput += "\n" + synthesizerOutput;

        return (fullOutput, synthesizerOutput);
    }
}
