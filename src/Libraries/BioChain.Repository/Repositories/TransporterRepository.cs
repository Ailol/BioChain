using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class TransporterRepository(BioChainDbContext db) : ITransporterRepository
{
    public Task<TransporterEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Transporters.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<TransporterEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default)
        => db.Transporters.FirstOrDefaultAsync(t => t.PersonId == personId && t.Code == code, ct);

    public Task<List<TransporterEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Transporters.Where(t => t.PersonId == personId).OrderBy(t => t.Code).ToListAsync(ct);

    public Task<List<TransporterEntity>> GetBySignalAsync(int signalId, CancellationToken ct = default)
        => db.Transporters.Where(t => t.SignalId == signalId).ToListAsync(ct);

    public async Task<TransporterEntity> UpsertAsync(TransporterEntity entity, CancellationToken ct = default)
    {
        var existing = await GetByCodeAsync(entity.PersonId, entity.Code, ct);
        if (existing is null)
        {
            db.Transporters.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.State = entity.State;
        existing.Clearance = entity.Clearance;
        existing.Embedding = entity.Embedding;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
