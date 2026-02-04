using System.ComponentModel;
using ModelContextProtocol.Server;
using Agents;

namespace McpAgentServer.Tools;

[McpServerToolType]
public class NeuroTools(MultiAgentService agentService)
{
    [McpServerTool(Name = "neuro_chat")]
    [Description("Run a Neuro group chat discussion. All 7 agents (Dopamine, Serotonin, Norepinephrine, GABA, Glutamate, Acetylcholine, NeuroSynthesizer) collaborate on the topic, analyzing it from their respective neurotransmitter perspectives.")]
    public async Task<string> NeuroChat(
        [Description("The topic for the neurochemical group discussion")] string topic,
        [Description("Maximum number of conversation turns (default: 8)")] int maxIterations = 8)
    {
        return await agentService.RunNeuroGroupChatDiscussionAsync(topic, maxIterations);
    }
}
