using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Models;

namespace Agents;

/// <summary>
/// Service that manages group chat orchestration with multiple specialized agents.
/// Uses local Ollama LLM for collaborative multi-agent discussions.
/// </summary>
public class MultiAgentService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly Dictionary<string, AgentProfile> _groupAgents;
    private readonly Dictionary<string, AgentProfile> _neuroAgents;
    private readonly Dictionary<string, AgentProfile> _hatsAgents;

    public MultiAgentService()
    {
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434";
        _modelName = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.2";

        _httpClient = new HttpClient { BaseAddress = new Uri(ollamaEndpoint) };

        _groupAgents = LoadProfiles("GroupAgents.json");
        _neuroAgents = LoadProfiles("NeuroAgents.json");
        _hatsAgents = LoadProfiles("HatsAgents.json");
    }

    private static Dictionary<string, AgentProfile> LoadProfiles(string filename)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", filename);
        if (!File.Exists(configPath))
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", filename);

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<Dictionary<string, AgentProfile>>(json)
            ?? throw new InvalidOperationException($"Failed to load {filename}");
    }

    /// <summary>
    /// Run neuro group chat where each neurotransmitter agent decides whether to add their perspective.
    /// Returns list of (neurotransmitter, explanation) for agents that chose to add.
    /// </summary>
    public async Task<List<NeuroDecision>> RunNeuroGroupChatAsync(string person, string topic, string context)
    {
        var decisions = new List<NeuroDecision>();
        var userMessage = $"Person: {person}\nTopic: {topic}\nContext: {context}";

        foreach (var (name, profile) in _neuroAgents)
        {
            try
            {
                var messages = new List<OllamaMessage>
                {
                    new() { Role = "user", Content = userMessage }
                };

                var response = await CallOllamaAsync(profile, messages);

                if (response.StartsWith("ADD:", StringComparison.OrdinalIgnoreCase))
                {
                    var explanation = response[4..].Trim();
                    decisions.Add(new NeuroDecision(name, explanation));
                }
            }
            catch { /* Skip agent on error */ }
        }

        return decisions;
    }

    /// <summary>
    /// Run a group chat discussion with all agents on a topic.
    /// </summary>
    public async Task<string> RunGroupChatAsync(string topic, int maxIterations = 6)
    {
        var agents = new[] { "Researcher", "Creative", "Critic", "Planner", "Synthesizer" };
        return await RunChatAsync(topic, agents, maxIterations);
    }

    /// <summary>
    /// Run a focused group chat with specific agent roles.
    /// </summary>
    public async Task<string> RunFocusedChatAsync(string topic, string[] agentRoles, int maxIterations = 4)
    {
        var agents = agentRoles.Select(NormalizeAgentName).ToList();
        agents.Add("Synthesizer"); // Always add synthesizer at the end
        return await RunChatAsync(topic, agents.ToArray(), maxIterations);
    }

    /// <summary>
    /// Run a Hats group chat with all 7 specialized agents for medicine/engineering systems thinking.
    /// </summary>
    public async Task<string> RunHatsGroupChatAsync(string topic, int maxIterations = 8)
    {
        var agents = new[] { "Researcher", "Creative", "Critic", "Planner", "Stabilizer", "SuperHat", "Synthesizer" };
        return await RunHatsChatAsync(topic, agents, maxIterations);
    }

    /// <summary>
    /// Run a Neuro group chat with all 6 neurotransmitter agents + synthesizer for neurochemical analysis.
    /// </summary>
    public async Task<string> RunNeuroGroupChatDiscussionAsync(string topic, int maxIterations = 8)
    {
        var agents = new[] { "Dopamine", "Serotonin", "Norepinephrine", "GABA", "Glutamate", "Acetylcholine", "NeuroSynthesizer" };
        return await RunNeuroChatAsync(topic, agents, maxIterations);
    }

    private async Task<string> RunNeuroChatAsync(string topic, string[] agents, int maxIterations)
    {
        var result = new StringBuilder();
        var conversationHistory = new List<OllamaMessage>();

        result.AppendLine($"# Neuro Group Discussion: {topic}");
        result.AppendLine();
        result.AppendLine("*Neurotransmitter Perspectives - Neurochemical Analysis*");
        result.AppendLine();
        result.AppendLine("---");
        result.AppendLine();

        // Initial user message
        conversationHistory.Add(new OllamaMessage
        {
            Role = "user",
            Content = $"Topic for neurochemical discussion: {topic}\n\nPlease analyze this topic from your neurotransmitter perspective. Discuss how your domain (the neurochemical system you govern) relates to this topic."
        });

        int iteration = 0;
        int agentIndex = 0;

        while (iteration < maxIterations)
        {
            var currentAgent = agents[agentIndex % agents.Length];
            var profile = _neuroAgents.GetValueOrDefault(currentAgent) ?? _neuroAgents["Dopamine"];

            try
            {
                var responseText = await CallOllamaAsync(profile, conversationHistory);

                result.AppendLine($"## **[{currentAgent}]** - {profile.Role}");
                result.AppendLine();
                result.AppendLine(responseText);
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();

                // Add to conversation history
                conversationHistory.Add(new OllamaMessage
                {
                    Role = "assistant",
                    Content = $"[{currentAgent}]: {responseText}"
                });

                // Check if synthesizer concluded
                if (currentAgent == "NeuroSynthesizer" && responseText.Contains("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"## **[{currentAgent}]** - {profile.Role}");
                result.AppendLine();
                result.AppendLine($"*Error: {ex.Message}*");
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();
            }

            agentIndex++;
            iteration++;
        }

        return result.ToString();
    }

    private async Task<string> RunHatsChatAsync(string topic, string[] agents, int maxIterations)
    {
        var result = new StringBuilder();
        var conversationHistory = new List<OllamaMessage>();

        result.AppendLine($"# Hats Group Discussion: {topic}");
        result.AppendLine();
        result.AppendLine("*Medicine meets Systems Engineering - The Hat Community*");
        result.AppendLine();
        result.AppendLine("---");
        result.AppendLine();

        // Initial user message
        conversationHistory.Add(new OllamaMessage
        {
            Role = "user",
            Content = $"Topic for discussion: {topic}\n\nPlease discuss this topic collaboratively, bridging medical and systems engineering perspectives."
        });

        int iteration = 0;
        int agentIndex = 0;

        while (iteration < maxIterations)
        {
            var currentAgent = agents[agentIndex % agents.Length];
            var profile = _hatsAgents.GetValueOrDefault(currentAgent) ?? _hatsAgents["Researcher"];

            try
            {
                var responseText = await CallOllamaAsync(profile, conversationHistory);

                result.AppendLine($"## **[{currentAgent}]** - {profile.Role}");
                result.AppendLine();
                result.AppendLine(responseText);
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();

                // Add to conversation history
                conversationHistory.Add(new OllamaMessage
                {
                    Role = "assistant",
                    Content = $"[{currentAgent}]: {responseText}"
                });

                // Check if synthesizer concluded
                if (currentAgent == "Synthesizer" && responseText.Contains("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"## **[{currentAgent}]** - {profile.Role}");
                result.AppendLine();
                result.AppendLine($"*Error: {ex.Message}*");
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();
            }

            agentIndex++;
            iteration++;
        }

        return result.ToString();
    }

    private async Task<string> RunChatAsync(string topic, string[] agents, int maxIterations)
    {
        var result = new StringBuilder();
        var conversationHistory = new List<OllamaMessage>();

        result.AppendLine($"# Group Chat Discussion: {topic}");
        result.AppendLine();
        result.AppendLine("---");
        result.AppendLine();

        // Initial user message
        conversationHistory.Add(new OllamaMessage
        {
            Role = "user",
            Content = $"Topic for discussion: {topic}\n\nPlease discuss this topic collaboratively."
        });

        int iteration = 0;
        int agentIndex = 0;

        while (iteration < maxIterations)
        {
            var currentAgent = agents[agentIndex % agents.Length];
            var profile = _groupAgents.GetValueOrDefault(currentAgent) ?? _groupAgents["Researcher"];

            try
            {
                var responseText = await CallOllamaAsync(profile, conversationHistory);

                result.AppendLine($"## **[{currentAgent}]**");
                result.AppendLine();
                result.AppendLine(responseText);
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();

                // Add to conversation history
                conversationHistory.Add(new OllamaMessage
                {
                    Role = "assistant",
                    Content = $"[{currentAgent}]: {responseText}"
                });

                // Check if synthesizer concluded
                if (currentAgent == "Synthesizer" && responseText.Contains("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"## **[{currentAgent}]**");
                result.AppendLine();
                result.AppendLine($"*Error: {ex.Message}*");
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();
            }

            agentIndex++;
            iteration++;
        }

        return result.ToString();
    }

    private async Task<string> CallOllamaAsync(AgentProfile profile, List<OllamaMessage> history)
    {
        var messages = new List<OllamaMessage>
        {
            new() { Role = "system", Content = profile.ToSystemPrompt() }
        };
        messages.AddRange(history);

        var request = new OllamaChatRequest
        {
            Model = _modelName,
            Messages = messages,
            Stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return result?.Message?.Content ?? "(No response)";
    }

    private static string NormalizeAgentName(string role) => role.ToLower() switch
    {
        "researcher" or "research" => "Researcher",
        "creative" or "brainstorm" => "Creative",
        "critic" or "critical" => "Critic",
        "planner" or "planning" => "Planner",
        "synthesizer" or "synthesis" => "Synthesizer",
        _ => "Researcher"
    };
}
