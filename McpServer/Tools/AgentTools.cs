using System.ComponentModel;
using ModelContextProtocol.Server;
using Agents;

namespace McpAgentServer.Tools;

[McpServerToolType]
public class AgentTools(MultiAgentService agentService)
{
    [McpServerTool(Name = "group_chat")]
    [Description("Run a full multi-agent group chat discussion. All 5 agents (Researcher, Creative, Critic, Planner, Synthesizer) collaborate on the topic using local Ollama LLM.")]
    public async Task<string> GroupChat(
        [Description("The topic for the group discussion")] string topic,
        [Description("Maximum number of conversation turns (default: 6)")] int maxIterations = 6)
    {
        return await agentService.RunGroupChatAsync(topic, maxIterations);
    }
}
