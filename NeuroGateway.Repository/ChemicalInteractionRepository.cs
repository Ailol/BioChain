using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class ChemicalInteractionRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public record InteractionDto(
        int Id,
        string SourceKey, string SourceLabel, string SourceLayer,
        string TargetKey, string TargetLabel, string TargetLayer,
        float ModFactor, string? Mechanism, string? Notes);

    public async Task<List<InteractionDto>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ChemicalInteractions
            .Join(db.Chemicals, i => i.SourceChemicalId, c => c.Id, (i, s) => new { i, Source = s })
            .Join(db.Chemicals, x => x.i.TargetChemicalId, c => c.Id, (x, t) => new InteractionDto(
                x.i.Id,
                x.Source.Key, x.Source.Label, x.Source.Layer,
                t.Key, t.Label, t.Layer,
                x.i.ModFactor, x.i.Mechanism, x.i.Notes))
            .ToListAsync();
    }

    public async Task<List<InteractionDto>> GetForChemicalAsync(string chemicalKey)
    {
        await using var db = await factory.CreateDbContextAsync();
        var chemId = await db.Chemicals
            .Where(c => c.Key == chemicalKey)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();

        if (chemId is null) return [];

        return await db.ChemicalInteractions
            .Where(i => i.SourceChemicalId == chemId || i.TargetChemicalId == chemId)
            .Join(db.Chemicals, i => i.SourceChemicalId, c => c.Id, (i, s) => new { i, Source = s })
            .Join(db.Chemicals, x => x.i.TargetChemicalId, c => c.Id, (x, t) => new InteractionDto(
                x.i.Id,
                x.Source.Key, x.Source.Label, x.Source.Layer,
                t.Key, t.Label, t.Layer,
                x.i.ModFactor, x.i.Mechanism, x.i.Notes))
            .ToListAsync();
    }

    public async Task<ChemicalInteractionEntity> CreateAsync(
        int sourceChemicalId, int targetChemicalId, float modFactor,
        string? mechanism = null, string? notes = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new ChemicalInteractionEntity
        {
            SourceChemicalId = sourceChemicalId,
            TargetChemicalId = targetChemicalId,
            ModFactor = modFactor,
            Mechanism = mechanism,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ChemicalInteractions.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> UpdateAsync(int id, float modFactor, string? mechanism, string? notes)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.ChemicalInteractions.FindAsync(id);
        if (entity is null) return false;

        entity.ModFactor = modFactor;
        entity.Mechanism = mechanism;
        entity.Notes = notes;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.ChemicalInteractions.FindAsync(id);
        if (entity is null) return false;

        db.ChemicalInteractions.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
