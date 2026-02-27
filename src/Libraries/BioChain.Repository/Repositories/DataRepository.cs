using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class DataRepository(BioChainDbContext db) : IDataRepository
{
    public Task<DataEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Events.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<DataEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Events.Where(d => d.PersonId == personId).OrderByDescending(d => d.CreatedOnUtc).ToListAsync(ct);

    public Task<List<DataEntity>> GetByPersonAndKindAsync(Guid personId, string kind, CancellationToken ct = default)
        => db.Events.Where(d => d.PersonId == personId && d.Kind == kind).OrderByDescending(d => d.CreatedOnUtc).ToListAsync(ct);

    public Task<List<DataEntity>> GetUnanalyzedAsync(Guid personId, CancellationToken ct = default)
        => db.Events.Where(d => d.PersonId == personId && !d.Analyzed).OrderBy(d => d.CreatedOnUtc).ToListAsync(ct);

    public async Task<DataEntity> CreateAsync(DataEntity entity, CancellationToken ct = default)
    {
        db.Events.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task MarkAnalyzedAsync(int id, CancellationToken ct = default)
    {
        await db.Events.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Analyzed, true), ct);
    }
}
