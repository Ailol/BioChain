using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

/// <summary>
/// Agent generation, group management, and group chat orchestration.
/// </summary>
public class AgentService
{
    private readonly Agents _agents;
    private readonly Chat _chat;
    private readonly PersonalityRepository _personalityRepo;
    private readonly AgentGroupRepository _agentGroupRepo;

    public AgentService(Agents agents, Chat chat, PersonalityRepository personalityRepo, AgentGroupRepository agentGroupRepo)
    {
        _agents = agents;
        _chat = chat;
        _personalityRepo = personalityRepo;
        _agentGroupRepo = agentGroupRepo;
    }

    /// <summary>
    /// Generate agents from a person's personality profile and save as a group.
    /// </summary>
    public async Task<(List<CustomAgent>? Agents, string? Error, List<string>? Suggestions, string? PersonName, Guid? GroupId)>
        GeneratePersonalityAgentsAsync(string person, int agentCount, string? groupName = null)
    {
        var personalityResult = await _personalityRepo.GetPersonalityAsync(person);
        if (personalityResult.Profile == null)
            return (null, "Person not found", personalityResult.Suggestions, null, null);

        var agents = await _agents.GenerateAgentsAsync(agentCount, profile: personalityResult.Profile);
        if (agents == null || agents.Count == 0)
            return (null, "Failed to generate agents from personality", null, null, null);

        var effectiveGroupName = groupName ?? person;
        var groupId = await _agentGroupRepo.CreateAgentGroupAsync(person, effectiveGroupName, agents);
        return (agents, null, null, personalityResult.Profile.Person, groupId);
    }

    /// <summary>
    /// Generate agents from a professional role and save as a group.
    /// </summary>
    public async Task<(List<CustomAgent>? Agents, string? Error, Guid? GroupId)>
        GenerateRoleAgentsAsync(string person, string role, int agentCount, string? groupName = null)
    {
        var agents = await _agents.GenerateAgentsAsync(agentCount, role: role);
        if (agents == null || agents.Count == 0)
            return (null, "Failed to generate agents from role", null);

        var effectiveGroupName = groupName ?? role;
        var groupId = await _agentGroupRepo.CreateAgentGroupAsync(person, effectiveGroupName, agents);
        return (agents, null, groupId);
    }

    /// <summary>
    /// Run a group chat with a previously generated agent group.
    /// </summary>
    public async Task<(string? Output, string? Error)> RunGroupChatAsync(string person, string topic, string? groupName = null, int maxIterations = 8)
    {
        var group = await _agentGroupRepo.GetAgentGroupAsync(person, groupName);
        if (group == null)
            return (null, $"No custom agent group found for '{person}'");

        var profiles = Agents.ToAgentProfiles(group.Agents);
        var (output, _) = await _chat.RunGroupChatAsync(profiles, topic, maxIterations: maxIterations);
        return (output, null);
    }
}
