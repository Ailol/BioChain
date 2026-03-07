using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class AnalysisRepository(BioChainDbContext db) : IAnalysisRepository
{
    public Task<AnalysisEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Analyses.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<AnalysisEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default)
        => db.Analyses.Where(p => p.SubjectId == subjectId).OrderByDescending(p => p.CreatedOnUtc).ToListAsync(ct);

    public Task<List<AnalysisEntity>> GetGlobalAsync(CancellationToken ct = default)
        => db.Analyses.Where(p => p.SubjectId == null).OrderByDescending(p => p.CreatedOnUtc).ToListAsync(ct);

    public async Task<AnalysisEntity> CreateAsync(AnalysisEntity entity, CancellationToken ct = default)
    {
        db.Analyses.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<AnalysisEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default)
        => db.Analyses
            .Where(p => p.ModuleId == moduleId && p.Tag == tag)
            .OrderByDescending(p => p.CreatedOnUtc)
            .ToListAsync(ct);
}
