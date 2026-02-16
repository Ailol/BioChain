using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class ChemicalRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<List<ChemicalEntity>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Chemicals.OrderBy(c => c.Id).ToListAsync();
    }

    public async Task<ChemicalEntity?> GetByKeyAsync(string key)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Chemicals.FirstOrDefaultAsync(c => c.Key == key);
    }

    public async Task<ChemicalEntity> CreateAsync(string key, string label, string layer)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new ChemicalEntity
        {
            Key = key,
            Label = label,
            Layer = layer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Chemicals.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> UpdateAsync(int id, string key, string label, string layer)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Chemicals.FindAsync(id);
        if (entity is null) return false;

        entity.Key = key;
        entity.Label = label;
        entity.Layer = layer;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Chemicals.FindAsync(id);
        if (entity is null) return false;

        db.Chemicals.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
