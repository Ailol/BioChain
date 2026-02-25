using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class DimensionRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public record DimensionWithAffinities(
        DimensionEntity Dimension,
        List<(string SignalKey, float Weight)> Affinities);

    public async Task<List<DimensionWithAffinities>> ListWithAffinitiesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var dimensions = await db.Dimensions.OrderBy(d => d.SortOrder).ToListAsync();
        var affinities = await db.DimensionSignalAffinities
            .Join(db.Signals, a => a.SignalId, c => c.Id, (a, c) => new { a.DimensionId, c.Key, a.Weight })
            .ToListAsync();

        var affinityLookup = affinities
            .GroupBy(a => a.DimensionId)
            .ToDictionary(g => g.Key, g => g.Select(x => (x.Key, x.Weight)).ToList());

        return dimensions.Select(d => new DimensionWithAffinities(
            d,
            affinityLookup.GetValueOrDefault(d.Id, [])
        )).ToList();
    }

    public async Task<DimensionEntity?> GetByNameAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Dimensions.FirstOrDefaultAsync(d => d.Name == name);
    }

    public async Task<DimensionEntity> CreateAsync(string name, string section, string category,
        string description, float workRelevance, float privateRelevance, int sortOrder)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new DimensionEntity
        {
            Name = name,
            Section = section,
            Category = category,
            Description = description,
            WorkRelevance = workRelevance,
            PrivateRelevance = privateRelevance,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Dimensions.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> UpdateAsync(int id, string name, string section, string category,
        string description, float workRelevance, float privateRelevance, int sortOrder)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Dimensions.FindAsync(id);
        if (entity is null) return false;

        entity.Name = name;
        entity.Section = section;
        entity.Category = category;
        entity.Description = description;
        entity.WorkRelevance = workRelevance;
        entity.PrivateRelevance = privateRelevance;
        entity.SortOrder = sortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Dimensions.FindAsync(id);
        if (entity is null) return false;

        db.Dimensions.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task SetAffinityAsync(int dimensionId, int signalId, float weight)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.DimensionSignalAffinities
            .FirstOrDefaultAsync(a => a.DimensionId == dimensionId && a.SignalId == signalId);

        if (existing is not null)
        {
            existing.Weight = weight;
        }
        else
        {
            db.DimensionSignalAffinities.Add(new DimensionSignalAffinityEntity
            {
                DimensionId = dimensionId,
                SignalId = signalId,
                Weight = weight
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task<bool> RemoveAffinityAsync(int dimensionId, int signalId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.DimensionSignalAffinities
            .FirstOrDefaultAsync(a => a.DimensionId == dimensionId && a.SignalId == signalId);
        if (existing is null) return false;

        db.DimensionSignalAffinities.Remove(existing);
        await db.SaveChangesAsync();
        return true;
    }
}
