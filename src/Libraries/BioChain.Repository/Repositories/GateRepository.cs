using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class GateRepository(BioChainDbContext db) : IGateRepository
{
    public Task<GateEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Gates.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<GateEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default)
        => db.Gates.FirstOrDefaultAsync(g => g.PersonId == personId && g.Code == code, ct);

    public Task<List<GateEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Gates.Where(g => g.PersonId == personId).OrderBy(g => g.Code).ToListAsync(ct);

    public Task<List<GateEntity>> GetByTypeAsync(Guid personId, string type, CancellationToken ct = default)
        => db.Gates.Where(g => g.PersonId == personId && g.Type == type).OrderBy(g => g.Code).ToListAsync(ct);

    public async Task<GateEntity> UpsertAsync(GateEntity entity, CancellationToken ct = default)
    {
        var existing = await GetByCodeAsync(entity.PersonId, entity.Code, ct);
        if (existing is null)
        {
            db.Gates.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.Type = entity.Type;
        existing.Threshold = entity.Threshold;
        existing.Expression = entity.Expression;
        existing.ParentId = entity.ParentId;
        existing.History = entity.History;
        existing.Latched = entity.Latched;
        existing.Embedding = entity.Embedding;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
