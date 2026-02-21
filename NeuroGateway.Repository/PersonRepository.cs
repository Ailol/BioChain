using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class PersonRepository(IDbContextFactory<PersonalityDbContext> factory, IUserContext userContext)
{
    public async Task<Guid> CreateAsync(string firstName)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new Entities.PersonEntity
        {
            OwnerId = userContext.UserId,
            FirstName = firstName,
            CreatedAt = DateTime.UtcNow
        };
        db.Persons.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<Guid> EnsureExistsAsync(string firstName)
    {
        var id = await GetIdAsync(firstName);
        return id ?? await CreateAsync(firstName);
    }

    public async Task<Guid?> GetIdAsync(string firstName)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.Persons
            .Where(p => p.FirstName.ToLower() == firstName.ToLower())
            .Where(p => p.OwnerId == userId
                || db.PersonShares.Any(s => s.PersonId == p.Id
                    && (s.SharedWithUserId == userId
                        || (email != null && s.SharedWithEmail == email))))
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<string>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.Persons
            .Where(p => p.OwnerId == userId
                || db.PersonShares.Any(s => s.PersonId == p.Id
                    && (s.SharedWithUserId == userId
                        || (email != null && s.SharedWithEmail == email))))
            .Select(p => p.FirstName)
            .OrderBy(n => n)
            .ToListAsync();
    }

    public async Task<List<string>> FindSimilarAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.first_name FROM person p
            WHERE (p.owner_id = @userId
                OR EXISTS (SELECT 1 FROM person_share ps
                           WHERE ps.person_id = p.id
                           AND (ps.shared_with_user_id = @userId
                                OR ps.shared_with_email = @email)))
              AND similarity(p.first_name, @name) > 0.3
            ORDER BY similarity(p.first_name, @name) DESC
            LIMIT 5
            """;
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);
        AddParam(cmd, "name", name);

        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        return results;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
