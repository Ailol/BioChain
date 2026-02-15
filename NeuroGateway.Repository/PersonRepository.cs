using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class PersonRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<Guid> CreateAsync(string firstName)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new Entities.PersonEntity { FirstName = firstName, CreatedAt = DateTime.UtcNow };
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
        return await db.Persons
            .Where(p => p.FirstName.ToLower() == firstName.ToLower())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<string>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Persons.Select(p => p.FirstName).OrderBy(n => n).ToListAsync();
    }

    public async Task<List<string>> FindSimilarAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT first_name FROM person
            WHERE similarity(first_name, @name) > 0.3
            ORDER BY similarity(first_name, @name) DESC
            LIMIT 5
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "name";
        p.Value = name;
        cmd.Parameters.Add(p);

        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        return results;
    }
}
