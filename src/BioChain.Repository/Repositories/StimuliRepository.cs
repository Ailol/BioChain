using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class StimuliRepository(BioChainDbContext db) : IStimuliRepository
{
    public Task<StimuliEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Stimuli.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<StimuliEntity>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default)
        => db.Stimuli.Where(d => d.SubjectId == subjectId).OrderByDescending(d => d.CreatedOnUtc).ToListAsync(ct);

    public Task<List<StimuliEntity>> GetBySubjectAndKindAsync(Guid subjectId, string kind, CancellationToken ct = default)
        => db.Stimuli.Where(d => d.SubjectId == subjectId && d.Kind == kind).OrderByDescending(d => d.CreatedOnUtc).ToListAsync(ct);

    public Task<List<StimuliEntity>> GetUnanalyzedAsync(Guid subjectId, CancellationToken ct = default)
        => db.Stimuli.Where(d => d.SubjectId == subjectId && !d.Analyzed).OrderBy(d => d.CreatedOnUtc).ToListAsync(ct);

    public async Task<StimuliEntity> CreateAsync(StimuliEntity entity, CancellationToken ct = default)
    {
        db.Stimuli.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task MarkAnalyzedAsync(int id, CancellationToken ct = default)
    {
        await db.Stimuli.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Analyzed, true), ct);
    }
}
