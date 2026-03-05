using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class ProtocolRepository(BioChainDbContext db) : IProtocolRepository
{
    public Task<ProtocolEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Protocols.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<ProtocolEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default)
        => db.Protocols.Where(p => p.SubjectId == subjectId).OrderByDescending(p => p.CreatedOnUtc).ToListAsync(ct);

    public Task<List<ProtocolEntity>> GetGlobalAsync(CancellationToken ct = default)
        => db.Protocols.Where(p => p.SubjectId == null).OrderByDescending(p => p.CreatedOnUtc).ToListAsync(ct);

    public async Task<ProtocolEntity> CreateAsync(ProtocolEntity entity, CancellationToken ct = default)
    {
        db.Protocols.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<ProtocolEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default)
        => db.Protocols
            .Where(p => p.ModuleId == moduleId && p.Tag == tag)
            .OrderByDescending(p => p.CreatedOnUtc)
            .ToListAsync(ct);
}
