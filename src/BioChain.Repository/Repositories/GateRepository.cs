using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class GateRepository(BioChainDbContext db) : IGateRepository
{
    public Task<GateEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Gates.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<GateEntity?> GetCurrentByCodeAsync(Guid subjectId, string code, CancellationToken ct = default)
        => db.Gates
            .Where(g => g.SubjectId == subjectId && g.Code == code)
            .OrderByDescending(g => g.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);

    public Task<List<GateEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default)
        => db.Gates.Where(g => g.SubjectId == subjectId).OrderBy(g => g.Code).ToListAsync(ct);

    public Task<List<GateEntity>> GetByTypeAsync(Guid subjectId, string type, CancellationToken ct = default)
        => db.Gates.Where(g => g.SubjectId == subjectId && g.Type == type).OrderBy(g => g.Code).ToListAsync(ct);

    public async Task<GateEntity> CreateAsync(GateEntity entity, CancellationToken ct = default)
    {
        db.Gates.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}
