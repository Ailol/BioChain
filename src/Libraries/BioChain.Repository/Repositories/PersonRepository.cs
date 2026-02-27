using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class PersonRepository(BioChainDbContext db) : IPersonRepository
{
    public Task<PersonEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Persons.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<PersonEntity?> GetByOwnerAndNameAsync(string ownerId, string name, CancellationToken ct = default)
        => db.Persons.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Name == name, ct);

    public Task<List<PersonEntity>> GetByOwnerAsync(string ownerId, CancellationToken ct = default)
        => db.Persons.Where(p => p.OwnerId == ownerId).OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<bool> HasAccessAsync(Guid personId, string userId, CancellationToken ct = default)
    {
        var isOwner = await db.Persons.AnyAsync(p => p.Id == personId && p.OwnerId == userId, ct);
        if (isOwner) return true;
        return await db.PersonShares.AnyAsync(s => s.PersonId == personId && s.SharedWithUserId == userId, ct);
    }

    public async Task<PersonEntity> CreateAsync(PersonEntity entity, CancellationToken ct = default)
    {
        db.Persons.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<PersonEntity> UpdateAsync(PersonEntity entity, CancellationToken ct = default)
    {
        entity.UpdatedOnUtc = DateTimeOffset.UtcNow;
        db.Persons.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.Persons.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }
}
