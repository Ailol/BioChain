using Microsoft.EntityFrameworkCore;
using Models;

namespace Repository;

/// <summary>
/// Data access for relationship_type table.
/// </summary>
public class RelationshipRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    /// <summary>
    /// Get or create a relationship type by name. Returns the name (lowercased) from the DB.
    /// </summary>
    public async Task<string> EnsureRelationshipTypeAsync(string name, string? description = null)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var lower = name.Trim().ToLower();

        var existing = await ctx.RelationshipTypes
            .FirstOrDefaultAsync(rt => rt.Name.ToLower() == lower);

        if (existing != null)
            return existing.Name;

        // Create new relationship type
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO relationship_type (name, description)
            VALUES ({lower}, {description ?? $"Auto-created relationship type: {name}"})
            ON CONFLICT (name) DO NOTHING
        """);
        return lower;
    }

    public async Task<List<RelationshipType>> ListRelationshipTypesAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.RelationshipTypes
            .OrderBy(rt => rt.Name)
            .Select(rt => new RelationshipType(rt.Id, rt.Name, rt.Description))
            .ToListAsync();
    }
}
