using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Repository;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class ChatTools(AgentService agentService, AgentGroupRepository agentGroupRepo)
{
    [McpServerTool(Name = "generate_personality_agents")]
    [Description("Generate 6-10 custom agents based on a person's personality profile. Fetches the personality, uses LLM to create specialized agents reflecting their traits, and saves as a reusable configuration.")]
    public async Task<string> GeneratePersonalityAgents(
        [Description("Person name to fetch personality for")] string person,
        [Description("Number of agents to generate (6-10, default: 7)")] int agentCount = 7,
        [Description("Optional group name (defaults to person name)")] string? groupName = null)
    {
        if (agentCount < 6 || agentCount > 10)
            return JsonSerializer.Serialize(new { error = "Agent count must be between 6 and 10" });

        var (agents, error, suggestions, personName, groupId) =
            await agentService.GeneratePersonalityAgentsAsync(person, agentCount, groupName);

        if (error != null)
            return suggestions?.Count > 0
                ? JsonSerializer.Serialize(new { error, suggestions })
                : JsonSerializer.Serialize(new { error });

        return JsonSerializer.Serialize(new
        {
            success = true,
            type = "personality",
            groupId,
            person = personName,
            groupName = groupName ?? person,
            agentCount = agents!.Count,
            agents = agents.Select(a => new { a.Name, a.Role }).ToList()
        });
    }

    [McpServerTool(Name = "generate_role_agents")]
    [Description("Generate 6-10 custom agents based on a professional role or archetype (e.g., 'senior engineers', 'product managers', 'medical specialists', 'startup founders'). Creates a team of experts who can analyze topics from that professional perspective.")]
    public async Task<string> GenerateRoleAgents(
        [Description("The role/archetype to base agents on (e.g., 'senior engineers', 'product managers', 'medical team')")] string role,
        [Description("Person name to associate the group with")] string person,
        [Description("Number of agents to generate (6-10, default: 7)")] int agentCount = 7,
        [Description("Optional group name (defaults to role name)")] string? groupName = null)
    {
        if (agentCount < 6 || agentCount > 10)
            return JsonSerializer.Serialize(new { error = "Agent count must be between 6 and 10" });

        var (agents, error, groupId) = await agentService.GenerateRoleAgentsAsync(person, role, agentCount, groupName);

        if (error != null)
            return JsonSerializer.Serialize(new { error });

        return JsonSerializer.Serialize(new
        {
            success = true,
            type = "role",
            groupId,
            person,
            role,
            groupName = groupName ?? role,
            agentCount = agents!.Count,
            agents = agents.Select(a => new { a.Name, a.Role }).ToList()
        });
    }

    // ===== Custom Agent Group Management =====

    [McpServerTool(Name = "list_custom_agent_groups")]
    [Description("List all available custom agent groups. Provide person name to get full configuration of a specific group.")]
    public async Task<string> ListCustomAgentGroups(
        [Description("Optional: person name to get full config of their group")] string? person = null,
        [Description("Optional: group name (defaults to person name)")] string? groupName = null)
    {
        if (person != null)
        {
            var group = await agentGroupRepo.GetAgentGroupAsync(person, groupName);
            return group != null
                ? JsonSerializer.Serialize(group)
                : JsonSerializer.Serialize(new { error = "Agent group not found" });
        }

        var groups = await agentGroupRepo.ListAgentGroupsAsync();
        return JsonSerializer.Serialize(new { groups });
    }

    [McpServerTool(Name = "run_custom_group_chat")]
    [Description("Run a group chat with a previously generated custom agent configuration based on a person's personality.")]
    public async Task<string> RunCustomGroupChat(
        [Description("Person name whose custom agents to use")] string person,
        [Description("The topic for the group discussion")] string topic,
        [Description("Optional group name (defaults to person name)")] string? groupName = null,
        [Description("Maximum number of conversation turns (default: 8)")] int maxIterations = 8)
    {
        var (output, error) = await agentService.RunGroupChatAsync(person, topic, groupName, maxIterations);
        return error != null ? JsonSerializer.Serialize(new { error }) : output!;
    }

    [McpServerTool(Name = "delete_custom_agent_group")]
    [Description("Remove a custom agent group.")]
    public async Task<string> DeleteCustomAgentGroup(
        [Description("Person name")] string person,
        [Description("Optional group name (defaults to person name)")] string? groupName = null)
    {
        var deleted = await agentGroupRepo.DeleteAgentGroupAsync(person, groupName);
        return deleted
            ? JsonSerializer.Serialize(new { success = true, deleted = person })
            : JsonSerializer.Serialize(new { success = false, error = "Agent group not found" });
    }
}
