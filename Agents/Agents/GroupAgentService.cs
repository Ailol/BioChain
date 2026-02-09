using Microsoft.Extensions.AI;
using Models;

namespace Agents;

/// <summary>
/// Service that manages group chat orchestration with multiple specialized agents.
/// Handles 3-layer biochemical analysis (NT, hormone, peptide) and group chat discussions.
/// </summary>
public class GroupAgentService
{
    private readonly LlmService _llm;
    private readonly int _maxParallelAgents;
    private readonly Dictionary<string, AgentProfile> _neuroAnalyzingAgents;
    private readonly Dictionary<string, AgentProfile> _hormoneAnalyzingAgents;
    private readonly Dictionary<string, AgentProfile> _peptideAnalyzingAgents;
    private readonly Dictionary<string, ResponderGroupConfig> _neuroChatAgents;

    public GroupAgentService(LlmService llm, AgentConfiguration config)
    {
        _llm = llm;
        _maxParallelAgents = config.MaxParallelAgents;
        _neuroAnalyzingAgents = ConfigLoader.LoadJson<Dictionary<string, AgentProfile>>("NeuroAnalyzingAgents.json");
        _hormoneAnalyzingAgents = ConfigLoader.LoadJson<Dictionary<string, AgentProfile>>("HormoneAnalyzingAgents.json");
        _peptideAnalyzingAgents = ConfigLoader.LoadJson<Dictionary<string, AgentProfile>>("PeptideAnalyzingAgents.json");
        _neuroChatAgents = ConfigLoader.LoadJson<Dictionary<string, ResponderGroupConfig>>("NeuroChatAgents.json");
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
                Conclusion = agent.IsSynthesizer,
                Layer = agent.Layer
            };
        }
        return profiles;
    }

    // ===== 3-Layer Biochemical Analysis =====

    /// <summary>
    /// Run NT analyzing agents — each decides SKIP or ADD (presence-based).
    /// </summary>
    public Task<List<BiochemicalDecision>> RunNeuroAnalysisAsync(string person, string topic, string context)
        => RunBiochemicalAnalysisAsync(person, topic, context, _neuroAnalyzingAgents);

    /// <summary>
    /// Run hormone analyzing agents — each decides SKIP or ADD (presence-based).
    /// </summary>
    public Task<List<BiochemicalDecision>> RunHormoneAnalysisAsync(string person, string topic, string context)
        => RunBiochemicalAnalysisAsync(person, topic, context, _hormoneAnalyzingAgents);

    /// <summary>
    /// Run peptide analyzing agents — each decides SKIP or ADD (presence-based).
    /// </summary>
    public Task<List<BiochemicalDecision>> RunPeptideAnalysisAsync(string person, string topic, string context)
        => RunBiochemicalAnalysisAsync(person, topic, context, _peptideAnalyzingAgents);

    /// <summary>
    /// Shared analysis method for all 3 biochemical layers.
    /// Parses ADD: reasoning format. Presence-based — no strength parsing.
    /// </summary>
    private async Task<List<BiochemicalDecision>> RunBiochemicalAnalysisAsync(
        string person, string topic, string context, Dictionary<string, AgentProfile> agentProfiles)
    {
        var userMessage = $"Person: {person}\nTopic: {topic}\nContext: {context}";
        var allResults = new List<BiochemicalDecision>();

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
                    {
                        var reasoning = cleaned[4..].Trim().TrimEnd('*');
                        return new BiochemicalDecision(name, reasoning);
                    }
                }
                catch { /* Skip agent on error */ }
                return null;
            });

            var results = await Task.WhenAll(tasks);
            allResults.AddRange(results.Where(d => d != null)!);
        }

        return allResults;
    }

    // ===== Neurorespond (3+1 agent flow) =====

    /// <summary>
    /// Run neurorespond: 3 biochemical layer agents in parallel + 1 synthesizer.
    /// Each layer agent gets {chemical} replaced with the person's top chemical from that layer.
    /// Returns exactly 4 NeuroResponse objects.
    /// </summary>
    public async Task<List<NeuroResponse>> RunNeuroRespondAsync(
        Dictionary<string, AgentProfile> profiles,
        string topic,
        string ntProfile, string hormoneProfile, string peptideProfile)
    {
        var synthesizer = profiles.FirstOrDefault(p => p.Value.Conclusion);
        var layerAgents = profiles.Where(p => !p.Value.Conclusion).ToList();

        // Map layer → formatted chemical profile string (all chemicals, not just top-1)
        var profileByLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["neurotransmitter"] = ntProfile,
            ["hormone"] = hormoneProfile,
            ["peptide"] = peptideProfile
        };

        // Map layer → display label for NeuroResponse.Source
        var sourceLabelByLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["neurotransmitter"] = "Neurotransmitters",
            ["hormone"] = "Hormones",
            ["peptide"] = "Peptides"
        };

        // Run 3 layer agents in parallel
        var tasks = layerAgents.Select(async kv =>
        {
            var (name, profile) = kv;
            var chemicals = profile.Layer != null && profileByLayer.TryGetValue(profile.Layer, out var p) ? p : "Unknown";
            var sourceLabel = profile.Layer != null && sourceLabelByLayer.TryGetValue(profile.Layer, out var s) ? s : "Unknown";
            var agentTopic = topic.Replace("{chemicals}", chemicals);

            // Replace {chemicals} in the style so the system prompt gets the full profile
            var agentProfile = new AgentProfile
            {
                Role = profile.Role,
                Style = profile.Style.Replace("{chemicals}", chemicals),
                MaxWords = profile.MaxWords,
                Conclusion = profile.Conclusion,
                Layer = profile.Layer
            };

            try
            {
                var messages = new List<ChatMessage> { new(ChatRole.User, agentTopic) };
                var response = await _llm.ChatWithProfileAsync(agentProfile, messages);
                var suggestion = ResponseService.ExtractSuggestion(response);
                return new NeuroResponse(sourceLabel, suggestion ?? response.Trim());
            }
            catch
            {
                return new NeuroResponse(sourceLabel, "[Agent failed to respond]");
            }
        });

        var layerResults = (await Task.WhenAll(tasks)).ToList();

        // Run synthesizer with all 3 outputs
        if (synthesizer.Key != null)
        {
            var outputsBlock = string.Join("\n", layerResults.Select(r => $"[{r.Source}]: {r.Message}"));
            var synthTopic = $"{topic}\n\nHere are the 3 biochemical agent responses:\n\n{outputsBlock}";

            var synthProfile = new AgentProfile
            {
                Role = synthesizer.Value.Role,
                Style = synthesizer.Value.Style,
                MaxWords = synthesizer.Value.MaxWords,
                Conclusion = synthesizer.Value.Conclusion
            };

            try
            {
                var synthMessages = new List<ChatMessage> { new(ChatRole.User, synthTopic) };
                var synthResponse = await _llm.ChatWithProfileAsync(synthProfile, synthMessages);
                var synthSuggestion = ResponseService.ExtractSuggestion(synthResponse);
                layerResults.Add(new NeuroResponse("Synthesizer", synthSuggestion ?? synthResponse.Trim()));
            }
            catch
            {
                layerResults.Add(new NeuroResponse("Synthesizer", "[Synthesizer failed to respond]"));
            }
        }

        return layerResults;
    }

    // ===== Group Chat =====

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
