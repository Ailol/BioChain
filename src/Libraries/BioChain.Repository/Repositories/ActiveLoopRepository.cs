using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class ActiveLoopRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<ActiveLoopEntity> UpsertAsync(ActiveLoopEntity entity)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.ActiveLoops
            .FirstOrDefaultAsync(l => l.PersonalityId == entity.PersonalityId
                && l.Name == entity.Name
                && l.Status != "resolved");

        if (existing is not null)
        {
            existing.Status = entity.Status;
            existing.Formula = entity.Formula;
            existing.InvolvedSignals = entity.InvolvedSignals;
            existing.FailureMode = entity.FailureMode;
            existing.Severity = entity.Severity;
            existing.LastConfirmedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }

        entity.FirstDetectedAt = DateTime.UtcNow;
        entity.LastConfirmedAt = DateTime.UtcNow;
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        db.ActiveLoops.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task<List<ActiveLoopEntity>> GetForPersonAsync(Guid personId, bool activeOnly = true)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.ActiveLoops.Where(l => l.PersonId == personId);
        if (activeOnly)
            query = query.Where(l => l.Status != "resolved");
        return await query.OrderByDescending(l => l.LastConfirmedAt).ToListAsync();
    }

    public async Task ResolveAsync(int loopId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.ActiveLoops.FindAsync(loopId);
        if (entity is null) return;
        entity.Status = "resolved";
        entity.ResolvedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
