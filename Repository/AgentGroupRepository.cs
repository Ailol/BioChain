using Microsoft.EntityFrameworkCore;
using Models;
using Entities = Repository.Entities;

namespace Repository;

/// <summary>
/// Data access for agent_group and agent tables — custom agent ensemble CRUD.
/// Supports both person-owned groups (person_id set) and shared/system groups (person_id NULL).
/// </summary>
public class AgentGroupRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<Guid> CreateAgentGroupAsync(string personName, string groupName, List<CustomAgent> agents)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var person = await ctx.Persons.FirstOrDefaultAsync(p => p.FirstName.ToLower() == personName.ToLower());
        if (person == null)
        {
            person = new Entities.Person { FirstName = personName };
            ctx.Persons.Add(person);
            await ctx.SaveChangesAsync();
        }

        // Upsert agent group — use partial unique index on (person_id, name) WHERE person_id IS NOT NULL
        var existing = await ctx.AgentGroups.FirstOrDefaultAsync(ag =>
            ag.PersonId == person.Id && ag.Name.ToLower() == groupName.ToLower());

        Guid groupId;
        if (existing != null)
        {
            existing.UpdatedAt = DateTime.UtcNow;
            groupId = existing.Id;
        }
        else
        {
            var newGroup = new Entities.AgentGroup { PersonId = person.Id, Name = groupName };
            ctx.AgentGroups.Add(newGroup);
            await ctx.SaveChangesAsync();
            groupId = newGroup.Id;
        }

        // Delete existing agents for this group (to allow regeneration)
        await ctx.Agents.Where(a => a.GroupId == groupId).ExecuteDeleteAsync();

        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            ctx.Agents.Add(new Entities.Agent
            {
                GroupId = groupId,
                Name = agent.Name,
                Role = agent.Role,
                Responsibilities = agent.Responsibilities,
                Style = agent.Style,
                MaxWords = agent.MaxWords,
                IsSynthesizer = agent.IsSynthesizer,
                SortOrder = i
            });
        }

        await ctx.SaveChangesAsync();
        return groupId;
    }

    public async Task<List<CustomAgentGroup>> ListAgentGroupsAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var groups = await ctx.AgentGroups
            .Include(ag => ag.Person)
            .Include(ag => ag.Agents.OrderBy(a => a.SortOrder))
            .OrderByDescending(ag => ag.CreatedAt)
            .ToListAsync();

        return groups.Select(ag => new CustomAgentGroup(
            ag.Id,
            ag.Person?.FirstName,
            ag.Name,
            ag.CreatedAt,
            ag.Agents.Count,
            ag.Agents.Select(a => a.Name).ToList()
        )).ToList();
    }

    public async Task<CustomAgentGroupDetail?> GetAgentGroupAsync(string personName, string? groupName = null)
    {
        var effectiveGroupName = groupName ?? personName;

        await using var ctx = await factory.CreateDbContextAsync();

        // First try person-owned group, then fall back to shared group
        var group = await ctx.AgentGroups
            .Include(ag => ag.Person)
            .Include(ag => ag.Agents.OrderBy(a => a.SortOrder))
            .FirstOrDefaultAsync(ag =>
                ag.Person != null &&
                ag.Person.FirstName.ToLower() == personName.ToLower() &&
                ag.Name.ToLower() == effectiveGroupName.ToLower());

        group ??= await ctx.AgentGroups
            .Include(ag => ag.Agents.OrderBy(a => a.SortOrder))
            .FirstOrDefaultAsync(ag =>
                ag.PersonId == null &&
                ag.Name.ToLower() == effectiveGroupName.ToLower());

        if (group == null) return null;

        var agents = group.Agents.Select(a => new CustomAgent(
            a.Name, a.Role, a.Responsibilities, a.Style, a.MaxWords, a.IsSynthesizer
        )).ToList();

        return new CustomAgentGroupDetail(group.Id, group.Person?.FirstName, group.Name, group.CreatedAt, agents);
    }

    public async Task<bool> DeleteAgentGroupAsync(string personName, string? groupName = null)
    {
        var effectiveGroupName = groupName ?? personName;

        await using var ctx = await factory.CreateDbContextAsync();
        var group = await ctx.AgentGroups
            .Include(ag => ag.Person)
            .FirstOrDefaultAsync(ag =>
                ag.Person != null &&
                ag.Person.FirstName.ToLower() == personName.ToLower() &&
                ag.Name.ToLower() == effectiveGroupName.ToLower());

        if (group == null) return false;

        ctx.AgentGroups.Remove(group);
        return await ctx.SaveChangesAsync() > 0;
    }
}
