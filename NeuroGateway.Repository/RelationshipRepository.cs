using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class RelationshipRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<List<(string Name, string? Description)>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.RelationshipTypes
            .OrderBy(r => r.Id)
            .Select(r => new { r.Name, r.Description })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Name, x.Description)).ToList());
    }

    public async Task<int> EnsureExistsAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.RelationshipTypes
            .Where(r => r.Name == name)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue) return existing.Value;

        var entity = new Entities.RelationshipTypeEntity { Name = name };
        db.RelationshipTypes.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }
}
