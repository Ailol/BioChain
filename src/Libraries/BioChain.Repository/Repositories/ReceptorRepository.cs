using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class ReceptorRepository(BioChainDbContext db) : IReceptorRepository
{
    public Task<ReceptorEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Receptors.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<ReceptorEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default)
        => db.Receptors.FirstOrDefaultAsync(r => r.PersonId == personId && r.Code == code, ct);

    public Task<List<ReceptorEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Receptors.Where(r => r.PersonId == personId).OrderBy(r => r.Code).ToListAsync(ct);

    public Task<List<ReceptorEntity>> GetBySignalAsync(int signalId, CancellationToken ct = default)
        => db.Receptors.Where(r => r.SignalId == signalId).ToListAsync(ct);

    public async Task<ReceptorEntity> UpsertAsync(ReceptorEntity entity, CancellationToken ct = default)
    {
        var existing = await GetByCodeAsync(entity.PersonId, entity.Code, ct);
        if (existing is null)
        {
            db.Receptors.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.State = entity.State;
        existing.Subtype = entity.Subtype;
        existing.Embedding = entity.Embedding;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
