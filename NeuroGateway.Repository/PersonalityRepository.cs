using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class PersonalityRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<int> EnsureExistsAsync(Guid personId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Personalities
            .Where(p => p.PersonId == personId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue) return existing.Value;

        var entity = new Entities.PersonalityEntity
        {
            PersonId = personId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Personalities.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<string?> GetCommunicationStyleAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Personalities
            .Join(db.Persons, pers => pers.PersonId, p => p.Id, (pers, p) => new { pers, p })
            .Where(x => x.p.FirstName.ToLower() == person.ToLower())
            .Select(x => x.pers.CommunicationStyle)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateCommunicationStyleAsync(string person, string style)
    {
        await using var db = await factory.CreateDbContextAsync();
        var personality = await db.Personalities
            .Join(db.Persons, pers => pers.PersonId, p => p.Id, (pers, p) => new { pers, p })
            .Where(x => x.p.FirstName.ToLower() == person.ToLower())
            .Select(x => x.pers)
            .FirstOrDefaultAsync();

        if (personality is null) return;
        personality.CommunicationStyle = style;
        personality.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
