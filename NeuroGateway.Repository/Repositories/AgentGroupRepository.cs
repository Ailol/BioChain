using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class AgentGroupRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<Guid> CreateAsync(Guid? personId, string name,
        List<(string Name, string Role, string[] Responsibilities, string Style, int MaxWords, bool IsSynthesizer, int SortOrder)> agents)
    {
        await using var db = await factory.CreateDbContextAsync();
        var group = new AgentGroupEntity
        {
            PersonId = personId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Agents = agents.Select(a => new AgentEntity
            {
                PersonId = personId,
                Name = a.Name,
                Role = a.Role,
                Responsibilities = a.Responsibilities,
                Style = a.Style,
                MaxWords = a.MaxWords,
                IsSynthesizer = a.IsSynthesizer,
                SortOrder = a.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList()
        };
        db.AgentGroups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    public async Task<AgentGroupEntity?> GetAsync(Guid? personId, string? name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.AgentGroups.Include(g => g.Agents.OrderBy(a => a.SortOrder)).AsQueryable();

        if (personId.HasValue)
            query = query.Where(g => g.PersonId == personId);
        else
            query = query.Where(g => g.PersonId == null);

        if (name is not null)
            query = query.Where(g => g.Name == name);

        return await query.FirstOrDefaultAsync();
    }

    public async Task<List<string>> ListAsync(Guid? personId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = personId.HasValue
            ? db.AgentGroups.Where(g => g.PersonId == personId)
            : db.AgentGroups.Where(g => g.PersonId == null);

        return await query.Select(g => g.Name).OrderBy(n => n).ToListAsync();
    }

    public async Task<bool> DeleteAsync(Guid? personId, string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var group = personId.HasValue
            ? await db.AgentGroups.FirstOrDefaultAsync(g => g.PersonId == personId && g.Name == name)
            : await db.AgentGroups.FirstOrDefaultAsync(g => g.PersonId == null && g.Name == name);

        if (group is null) return false;
        db.AgentGroups.Remove(group);
        await db.SaveChangesAsync();
        return true;
    }
}
