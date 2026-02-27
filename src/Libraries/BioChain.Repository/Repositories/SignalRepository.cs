using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class SignalRepository(BioChainDbContext db) : ISignalRepository
{
    public Task<SignalEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Signals.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<SignalEntity?> GetByCodeAsync(Guid personId, string code, string? region = null, CancellationToken ct = default)
        => db.Signals.FirstOrDefaultAsync(s => s.PersonId == personId && s.Code == code && s.Region == region, ct);

    public Task<List<SignalEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Signals.Where(s => s.PersonId == personId).OrderBy(s => s.Code).ToListAsync(ct);

    public Task<List<SignalEntity>> GetByTypeAsync(Guid personId, string type, CancellationToken ct = default)
        => db.Signals.Where(s => s.PersonId == personId && s.Type == type).OrderBy(s => s.Code).ToListAsync(ct);

    public async Task<SignalEntity> UpsertAsync(SignalEntity entity, CancellationToken ct = default)
    {
        var existing = await GetByCodeAsync(entity.PersonId, entity.Code, entity.Region, ct);
        if (existing is null)
        {
            db.Signals.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.State = entity.State;
        existing.Baseline = entity.Baseline;
        existing.TauMin = entity.TauMin;
        existing.TauMax = entity.TauMax;
        existing.Embedding = entity.Embedding;
        existing.UpdatedOnUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
