using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class PersonShareRepository(BioChainDbContext db) : IPersonShareRepository
{
    public Task<List<PersonShareEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default)
        => db.PersonShares.Where(s => s.SubjectId == subjectId).ToListAsync(ct);

    public Task<List<PersonShareEntity>> GetByUserAsync(string userId, CancellationToken ct = default)
        => db.PersonShares.Where(s => s.SharedWithUserId == userId).ToListAsync(ct);

    public async Task<PersonShareEntity> CreateAsync(PersonShareEntity entity, CancellationToken ct = default)
    {
        db.PersonShares.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(Guid subjectId, string sharedWithEmail, CancellationToken ct = default)
    {
        await db.PersonShares
            .Where(s => s.SubjectId == subjectId && s.SharedWithEmail == sharedWithEmail)
            .ExecuteDeleteAsync(ct);
    }

    public async Task ResolveSharesAsync(string userId, string email, CancellationToken ct = default)
    {
        await db.PersonShares
            .Where(s => s.SharedWithEmail == email && s.SharedWithUserId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SharedWithUserId, userId), ct);
    }
}
