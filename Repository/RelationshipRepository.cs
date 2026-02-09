using Microsoft.EntityFrameworkCore;
using Models;

namespace Repository;

/// <summary>
/// Data access for relationship_type and relationship_profile tables.
/// Handles profile CRUD and staleness detection for lazy recomputation.
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

    public async Task<RelationshipProfile?> GetRelationshipProfileAsync(string person, string relationshipType)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entity = await ctx.RelationshipProfiles
            .Include(rp => rp.Person)
            .Include(rp => rp.RelationshipType)
            .FirstOrDefaultAsync(rp =>
                rp.Person.FirstName.ToLower() == person.ToLower() &&
                rp.RelationshipType.Name.ToLower() == relationshipType.ToLower());

        if (entity == null) return null;

        return new RelationshipProfile(
            entity.Id,
            entity.Person.FirstName,
            entity.RelationshipType.Name,
            VectorMath.ToFloatArray(entity.CompatibilityVector),
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    public async Task UpsertRelationshipProfileAsync(string person, string relationshipType, string? compatibilityVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO relationship_profile (person_id, relationship_type_id, compatibility_vector)
            SELECT p.id, rt.id, {compatibilityVector}::vector
            FROM person p, relationship_type rt
            WHERE LOWER(p.first_name) = LOWER({person}) AND LOWER(rt.name) = LOWER({relationshipType})
            ON CONFLICT (person_id, relationship_type_id)
            DO UPDATE SET compatibility_vector = EXCLUDED.compatibility_vector, updated_at = NOW()
        """);
    }

    public async Task<List<StaleRelationshipProfile>> GetStaleRelationshipProfilesAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Database.SqlQueryRaw<StaleRelationshipProfile>("""
            SELECT rt.name AS "RelationshipType", rp.updated_at AS "ProfileUpdatedAt",
                   MAX(pe.updated_at) AS "LatestTraitUpdate"
            FROM relationship_profile rp
            JOIN person pr ON pr.id = rp.person_id
            JOIN relationship_type rt ON rt.id = rp.relationship_type_id
            JOIN personality pe ON pe.person_id = pr.id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
            GROUP BY rt.name, rp.updated_at
            HAVING rp.updated_at < MAX(pe.updated_at)
        """, person).ToListAsync();
    }

    public async Task<List<RelationshipProfileSummary>> ListRelationshipProfilesAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.RelationshipProfiles
            .Include(rp => rp.Person)
            .Include(rp => rp.RelationshipType)
            .Where(rp => rp.Person.FirstName.ToLower() == person.ToLower())
            .OrderBy(rp => rp.RelationshipType.Name)
            .Select(rp => new RelationshipProfileSummary(
                rp.RelationshipType.Name,
                rp.UpdatedAt,
                rp.CompatibilityVector != null
            ))
            .ToListAsync();
    }
}
