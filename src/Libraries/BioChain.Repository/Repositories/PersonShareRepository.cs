using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository;

public class PersonShareRepository(IDbContextFactory<PersonalityDbContext> factory, IUserContext userContext)
{
    public async Task ShareAsync(Guid personId, string email)
    {
        await using var db = await factory.CreateDbContextAsync();

        // verify the current user owns this person
        var person = await db.Persons.FirstOrDefaultAsync(p => p.Id == personId && p.OwnerId == userContext.UserId);
        if (person is null)
            throw new InvalidOperationException("Person not found or you are not the owner");

        var existing = await db.PersonShares
            .AnyAsync(s => s.PersonId == personId && s.SharedWithEmail == email.ToLower());
        if (existing) return;

        // try to resolve user_id from email if they already have a person in the system
        var resolvedUserId = await db.Persons
            .Where(p => p.Email != null && p.Email.ToLower() == email.ToLower())
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync();

        db.PersonShares.Add(new Entities.PersonShareEntity
        {
            PersonId = personId,
            SharedWithEmail = email.ToLower(),
            SharedWithUserId = resolvedUserId,
            SharedByUserId = userContext.UserId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task UnshareAsync(Guid personId, string email)
    {
        await using var db = await factory.CreateDbContextAsync();
        var share = await db.PersonShares
            .FirstOrDefaultAsync(s => s.PersonId == personId
                && s.SharedWithEmail == email.ToLower()
                && s.SharedByUserId == userContext.UserId);
        if (share is not null)
        {
            db.PersonShares.Remove(share);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<(string Email, DateTime SharedAt)>> ListSharesAsync(Guid personId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PersonShares
            .Where(s => s.PersonId == personId)
            .Select(s => new { s.SharedWithEmail, s.CreatedAt })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.SharedWithEmail, x.CreatedAt)).ToList());
    }

    // called after login to match email → user_id on pending shares
    public async Task ResolveSharesAsync(string userId, string email)
    {
        await using var db = await factory.CreateDbContextAsync();
        var pending = await db.PersonShares
            .Where(s => s.SharedWithEmail == email.ToLower() && s.SharedWithUserId == null)
            .ToListAsync();

        foreach (var share in pending)
            share.SharedWithUserId = userId;

        if (pending.Count > 0)
            await db.SaveChangesAsync();
    }
}
