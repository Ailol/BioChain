using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class ProtocolRepository(BioChainDbContext db) : IProtocolRepository
{
    public Task<ProtocolEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Protocols.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<ProtocolEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Protocols.Where(p => p.PersonId == personId).OrderByDescending(p => p.CreatedOnUtc).ToListAsync(ct);

    public Task<List<ProtocolEntity>> GetGlobalAsync(CancellationToken ct = default)
        => db.Protocols.Where(p => p.PersonId == null).OrderByDescending(p => p.CreatedOnUtc).ToListAsync(ct);

    public async Task<ProtocolEntity> CreateAsync(ProtocolEntity entity, CancellationToken ct = default)
    {
        db.Protocols.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<ProtocolEntity> UpdateAsync(ProtocolEntity entity, CancellationToken ct = default)
    {
        entity.UpdatedOnUtc = DateTimeOffset.UtcNow;
        db.Protocols.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}
