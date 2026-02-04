using System.ComponentModel;
using ModelContextProtocol.Server;
using Agents;

namespace McpAgentServer.Tools;

[McpServerToolType]
public class HatsTools(MultiAgentService agentService)
{
    [McpServerTool(Name = "hats_chat")]
    [Description("Run a Hats group chat discussion. All 7 agents (Researcher, Creative, Critic, Planner, Stabilizer, SuperHat, Synthesizer) collaborate on the topic, bridging medicine and systems engineering perspectives using the Hat Community vocabulary.")]
    public async Task<string> HatsChat(
        [Description("The topic for the Hats group discussion")] string topic,
        [Description("Maximum number of conversation turns (default: 8)")] int maxIterations = 8)
    {
        return await agentService.RunHatsGroupChatAsync(topic, maxIterations);
    }
}
