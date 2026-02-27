using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class LimiterRepository(BioChainDbContext db) : ILimiterRepository
{
    public Task<LimiterEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Limiters.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<LimiterEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default)
        => db.Limiters.FirstOrDefaultAsync(l => l.PersonId == personId && l.Code == code, ct);

    public Task<List<LimiterEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Limiters.Where(l => l.PersonId == personId).OrderBy(l => l.Code).ToListAsync(ct);

    public Task<List<LimiterEntity>> GetBottlenecksAsync(Guid personId, CancellationToken ct = default)
        => db.Limiters.Where(l => l.PersonId == personId && l.RateLimiting).OrderBy(l => l.Code).ToListAsync(ct);

    public async Task<LimiterEntity> UpsertAsync(LimiterEntity entity, CancellationToken ct = default)
    {
        var existing = await GetByCodeAsync(entity.PersonId, entity.Code, ct);
        if (existing is null)
        {
            db.Limiters.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.TargetId = entity.TargetId;
        existing.Reaction = entity.Reaction;
        existing.RateLimiting = entity.RateLimiting;
        existing.Activity = entity.Activity;
        existing.Embedding = entity.Embedding;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
