using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class AgentTemplateRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<List<AgentTemplateEntity>> GetByCategoryAsync(string category)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.AgentTemplates
            .Where(t => t.Category == category)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }

    public async Task<List<AgentTemplateEntity>> GetByGroupAsync(string groupName)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.AgentTemplates
            .Where(t => t.Category == "neurochat" && t.GroupName == groupName)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }
}
