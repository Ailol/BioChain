using Microsoft.EntityFrameworkCore;
using Repository.Entities;

namespace Repository;

/// <summary>
/// Data access for the person table — CRUD and fuzzy/vector matching.
/// </summary>
public class PersonRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<List<string>> ListPersonsAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Persons
            .OrderBy(p => p.FirstName)
            .Select(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<bool> CreatePersonAsync(string name)
    {
        if (await PersonExistsAsync(name)) return false;
        await using var ctx = await factory.CreateDbContextAsync();
        ctx.Persons.Add(new Person { FirstName = name });
        return await ctx.SaveChangesAsync() > 0;
    }

    public async Task EnsurePersonExistsAsync(string name)
    {
        if (await PersonExistsAsync(name)) return;
        await using var ctx = await factory.CreateDbContextAsync();
        ctx.Persons.Add(new Person { FirstName = name });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> PersonExistsAsync(string name)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Persons.AnyAsync(p => p.FirstName.ToLower() == name.ToLower());
    }

    public async Task<List<string>> FindSimilarPersonsAsync(string search)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Database.SqlQueryRaw<string>("""
            SELECT first_name AS "Value"
            FROM person
            WHERE similarity(LOWER(first_name), LOWER(@p0)) > 0.3
            ORDER BY similarity(LOWER(first_name), LOWER(@p0)) DESC
            LIMIT 5
        """, search).ToListAsync();
    }

    /// <summary>
    /// Match a person by name (exact) or by embedding similarity fallback.
    /// Returns (matchedName, matchMethod).
    /// </summary>
    public async Task<(string Person, string MatchedBy)> MatchPersonAsync(string? personName, string? embeddingVector)
    {
        if (!string.IsNullOrWhiteSpace(personName))
        {
            if (await PersonExistsAsync(personName)) return (personName, "name");
        }

        if (!string.IsNullOrWhiteSpace(embeddingVector))
        {
            var bestMatch = await FindBestPersonByEmbeddingAsync(embeddingVector);
            if (bestMatch != null)
                return (bestMatch, string.IsNullOrWhiteSpace(personName) ? "embedding" : "name_fallback");
        }

        if (!string.IsNullOrWhiteSpace(personName))
            return (personName, "name_unverified");

        throw new InvalidOperationException("Could not identify person. Please provide a name or ensure profiles exist.");
    }

    public async Task<Guid?> GetPersonIdAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Persons
            .Where(p => p.FirstName.ToLower() == person.ToLower())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<string?> FindBestPersonByEmbeddingAsync(string embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var result = await ctx.Database.SqlQueryRaw<string>("""
            SELECT pr.first_name AS "Value"
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            WHERE p.embedding IS NOT NULL
            GROUP BY pr.first_name
            ORDER BY MIN(p.embedding <=> @p0::vector)
            LIMIT 1
        """, embeddingVector).ToListAsync();

        return result.FirstOrDefault();
    }
}
