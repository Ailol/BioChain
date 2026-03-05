using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class SubjectRepository(BioChainDbContext db) : ISubjectRepository
{
    public Task<SubjectEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Subjects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<SubjectEntity?> GetByOwnerAndNameAsync(string ownerId, string name, CancellationToken ct = default)
        => db.Subjects.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Name == name, ct);

    public Task<List<SubjectEntity>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => db.Subjects.Where(p => p.OwnerId == ownerId).OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<bool> HasAccessAsync(Guid subjectId, string userId, CancellationToken ct = default)
    {
        var isOwner = await db.Subjects.AnyAsync(p => p.Id == subjectId && p.OwnerId == userId, ct);
        if (isOwner) return true;
        return await db.PersonShares.AnyAsync(s => s.SubjectId == subjectId && s.SharedWithUserId == userId, ct);
    }

    public async Task<SubjectEntity> CreateAsync(SubjectEntity entity, CancellationToken ct = default)
    {
        db.Subjects.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<SubjectEntity> UpdateAsync(SubjectEntity entity, CancellationToken ct = default)
    {
        db.Subjects.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.Subjects.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }
}
