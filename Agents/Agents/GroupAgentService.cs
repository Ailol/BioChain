using System.Text.Json;
using Microsoft.Extensions.AI;
using Models;

namespace Agents;

/// <summary>
/// Service that manages group chat orchestration with multiple specialized agents.
/// Handles neuro analyzing (personality creation) and group chat discussions.
/// </summary>
public class GroupAgentService
{
    private readonly LlmService _llm;
    private readonly int _maxParallelAgents;
    private readonly Dictionary<string, AgentProfile> _neuroAnalyzingAgents;
    private readonly Dictionary<string, AgentProfile> _neuroCVAnalyzingAgents;
    private readonly Dictionary<string, ResponderGroupConfig> _neuroChatAgents;

    public GroupAgentService(LlmService llm, AgentConfiguration config)
    {
        _llm = llm;
        _maxParallelAgents = config.MaxParallelAgents;
        _neuroAnalyzingAgents = LoadConfig<Dictionary<string, AgentProfile>>("NeuroAnalyzingAgents.json");
        _neuroCVAnalyzingAgents = LoadConfig<Dictionary<string, AgentProfile>>("NeuroCVAnalyzingAgents.json");
        _neuroChatAgents = LoadConfig<Dictionary<string, ResponderGroupConfig>>("NeuroChatAgents.json");
    }

    private static T LoadConfig<T>(string filename) where T : new()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", filename);
        if (!File.Exists(configPath))
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", filename);

        if (!File.Exists(configPath))
            return new T();

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new T();
    }

    /// <summary>
    /// Get agent profiles for a responder group from NeuroChatAgents config.
    /// </summary>
    public Dictionary<string, AgentProfile>? GetNeuroChatProfiles(ResponderGroup group)
    {
        var groupName = group.ToString();
        if (!_neuroChatAgents.TryGetValue(groupName, out var groupConfig))
            return null;

        var profiles = new Dictionary<string, AgentProfile>();
        foreach (var agent in groupConfig.Agents)
        {
            profiles[agent.Name] = new AgentProfile
            {
                Role = agent.Role,
                Style = agent.Style,
                MaxWords = agent.MaxWords,
                Conclusion = agent.IsSynthesizer
            };
        }
        return profiles;
    }

    /// <summary>
    /// Run neuro analyzing agents in parallel — each NT agent decides whether to add their perspective (SKIP/ADD).
    /// </summary>
    public Task<List<NeuroDecision>> RunNeuroAnalysisAsync(string person, string topic, string context)
        => RunNeuroAnalysisAsync(person, topic, context, _neuroAnalyzingAgents);

    /// <summary>
    /// Run CV-specialized neuro analyzing agents — reads between the lines of professional CVs.
    /// </summary>
    public Task<List<NeuroDecision>> RunNeuroCVAnalysisAsync(string person, string topic, string context)
        => RunNeuroAnalysisAsync(person, topic, context, _neuroCVAnalyzingAgents);

    private async Task<List<NeuroDecision>> RunNeuroAnalysisAsync(
        string person, string topic, string context, Dictionary<string, AgentProfile> agentProfiles)
    {
        var userMessage = $"Person: {person}\nTopic: {topic}\nContext: {context}";
        var allResults = new List<NeuroDecision>();

        // Process agents in batches to avoid overwhelming the LLM server
        foreach (var batch in agentProfiles.Chunk(_maxParallelAgents))
        {
            var tasks = batch.Select(async kv =>
            {
                var (name, profile) = kv;
                try
                {
                    var messages = new List<ChatMessage> { new(ChatRole.User, userMessage) };
                    var response = await _llm.ChatWithProfileAsync(profile, messages);

                    // Strip markdown bold/italic that some models wrap around ADD:/SKIP
                    var cleaned = response.TrimStart('*', ' ', '#');
                    if (cleaned.StartsWith("ADD:", StringComparison.OrdinalIgnoreCase))
                        return new NeuroDecision(name, cleaned[4..].Trim().TrimEnd('*'));
                }
                catch { /* Skip agent on error */ }
                return null;
            });

            var results = await Task.WhenAll(tasks);
            allResults.AddRange(results.Where(d => d != null)!);
        }

        return allResults;
    }

    /// <summary>
    /// Run a group chat. When synthesizerInstruction is provided, agents run in parallel
    /// and the synthesizer combines their outputs. Without it, agents run sequentially
    /// with shared conversation history.
    /// Returns (FullOutput, SynthesizerOutput) — SynthesizerOutput is null if no synthesizer.
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
            // Parallel mode: agents are independent, all get the same topic
            var tasks = chatAgents.Select(async kv =>
            {
                try
                {
                    var messages = new List<ChatMessage> { new(ChatRole.User, topic) };
                    var response = await _llm.ChatWithProfileAsync(kv.Value, messages);
                    return (Name: kv.Key, Response: response);
                }
                catch { return (Name: kv.Key, Response: (string?)null); }
            });

            var results = await Task.WhenAll(tasks);
            agentOutputs = results.Where(r => r.Response != null).Select(r => (r.Name, r.Response!)).ToList();
        }
        else
        {
            // Sequential mode: shared conversation history
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

        // Run synthesizer if instruction provided
        string? synthesizerOutput = null;
        if (synthesizer.Key != null && agentOutputs.Count > 0)
        {
            var outputsBlock = string.Join("\n", agentOutputs.Select(a => $"[{a.Name}]: {a.Response}"));

            var synthHistory = new List<ChatMessage>
            {
                new(ChatRole.User, $"{topic}\n\nHere are the agent responses:\n\n{outputsBlock}\n\n{synthesizerInstruction}")
            };

            try
            {
                synthesizerOutput = await _llm.ChatWithProfileAsync(synthesizer.Value, synthHistory);
            }
            catch { /* Synthesizer failed */ }
        }

        var fullOutput = string.Join("\n", agentOutputs.Select(a => a.Response));
        if (synthesizerOutput != null)
            fullOutput += "\n" + synthesizerOutput;

        return (fullOutput, synthesizerOutput);
    }
}
