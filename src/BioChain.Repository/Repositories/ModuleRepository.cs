using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class ModuleRepository(BioChainDbContext db) : IModuleRepository
{
    public Task<ModuleEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Modules.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<List<ModuleEntity>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default)
        => db.Modules
            .Where(m => m.SubjectId == subjectId)
            .OrderByDescending(m => m.CreatedOnUtc)
            .ToListAsync(ct);

    public Task<ModuleEntity?> GetCurrentByCodeAsync(Guid subjectId, string code, CancellationToken ct = default)
        => db.Modules
            .Where(m => m.SubjectId == subjectId && m.Code == code)
            .OrderByDescending(m => m.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);

    public Task<List<ModuleEntity>> GetByAgentTypeAsync(Guid subjectId, string agentType, CancellationToken ct = default)
        => db.Modules
            .Where(m => m.SubjectId == subjectId && m.AgentType == agentType)
            .ToListAsync(ct);

    public Task<List<ModuleEntity>> GetByNamespaceAsync(Guid subjectId, string ns, CancellationToken ct = default)
        => db.Modules
            .Where(m => m.SubjectId == subjectId && m.Namespace == ns)
            .ToListAsync(ct);

    public async Task<ModuleEntity> CreateAsync(ModuleEntity entity, CancellationToken ct = default)
    {
        db.Modules.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdatePropertiesAsync(int moduleId, string propertiesJson, CancellationToken ct = default)
    {
        var entity = await db.Modules.FindAsync([moduleId], ct)
            ?? throw new InvalidOperationException($"Module {moduleId} not found");
        entity.Properties = propertiesJson;
        await db.SaveChangesAsync(ct);
    }
}
