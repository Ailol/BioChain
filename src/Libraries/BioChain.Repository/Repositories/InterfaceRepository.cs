using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class InterfaceRepository(BioChainDbContext db) : IInterfaceRepository
{
    public Task<InterfaceEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Interfaces.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<InterfaceEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default)
        => db.Interfaces.FirstOrDefaultAsync(i => i.PersonId == personId && i.Code == code, ct);

    public Task<List<InterfaceEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Interfaces.Where(i => i.PersonId == personId).OrderBy(i => i.Code).ToListAsync(ct);

    public Task<List<InterfaceEntity>> GetActiveAsync(Guid personId, CancellationToken ct = default)
        => db.Interfaces.Where(i => i.PersonId == personId && i.Active).OrderBy(i => i.Code).ToListAsync(ct);

    public async Task<InterfaceEntity> UpsertAsync(InterfaceEntity entity, CancellationToken ct = default)
    {
        var existing = await GetByCodeAsync(entity.PersonId, entity.Code, ct);
        if (existing is null)
        {
            db.Interfaces.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.SourceRegion = entity.SourceRegion;
        existing.TargetRegion = entity.TargetRegion;
        existing.Pathway = entity.Pathway;
        existing.Active = entity.Active;
        existing.Embedding = entity.Embedding;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
