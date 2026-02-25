using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class TrajectoryRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<TrajectoryEntity> CreateAsync(TrajectoryEntity entity)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Trajectories.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task AddPhaseAsync(TrajectoryPhaseEntity phase)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.TrajectoryPhases.Add(phase);
        await db.SaveChangesAsync();
    }

    public async Task<List<TrajectoryEntity>> GetActiveAsync(Guid personId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Trajectories
            .Where(t => t.PersonId == personId && t.Status == "active")
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<TrajectoryPhaseEntity>> GetPhasesAsync(int trajectoryId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TrajectoryPhases
            .Where(p => p.TrajectoryId == trajectoryId)
            .OrderBy(p => p.PhaseNumber)
            .ToListAsync();
    }

    public async Task ResolveAsync(int trajectoryId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Trajectories.FindAsync(trajectoryId);
        if (entity is null) return;
        entity.Status = "resolved";
        entity.ResolvedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
