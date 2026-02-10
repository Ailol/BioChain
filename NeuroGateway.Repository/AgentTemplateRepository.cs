using Microsoft.EntityFrameworkCore;
using NeuroGateway.Models;

namespace NeuroGateway.Repository;

/// <summary>
/// Data access for agent_template table — system-level agent configurations (not user-generated).
/// </summary>
public class AgentTemplateRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    /// <summary>
    /// Load analyzing agents by category (e.g., "analyzing_neurotransmitter", "analyzing_hormone", "analyzing_peptide").
    /// Returns Dictionary keyed by agent name → AgentProfile.
    /// </summary>
    public async Task<Dictionary<string, AgentProfile>> GetAnalyzingAgentsAsync(string category)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var templates = await ctx.AgentTemplates
            .Where(t => t.Category == category)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return templates.ToDictionary(t => t.Name, t => new AgentProfile
        {
            Role = t.Role,
            Responsibilities = t.Responsibilities ?? [],
            Style = t.Style,
            MaxWords = t.MaxWords,
            Layer = t.Layer,
            Conclusion = false
        });
    }

    /// <summary>
    /// Load all neurochat agents grouped by relationship type.
    /// Returns Dictionary keyed by group name (e.g., "Dating") → ResponderGroupConfig.
    /// </summary>
    public async Task<Dictionary<string, ResponderGroupConfig>> GetNeuroChatAgentsAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var templates = await ctx.AgentTemplates
            .Where(t => t.Category == "neurochat")
            .OrderBy(t => t.GroupName)
            .ThenBy(t => t.SortOrder)
            .ToListAsync();

        return templates
            .GroupBy(t => t.GroupName ?? "Unknown")
            .ToDictionary(g => g.Key, g => new ResponderGroupConfig
            {
                Agents = g.Select(t => new ResponderGroupAgent
                {
                    Name = t.Name,
                    Layer = t.Layer,
                    Role = t.Role,
                    Style = t.Style,
                    MaxWords = t.MaxWords,
                    IsSynthesizer = t.IsSynthesizer
                }).ToList()
            });
    }
}
