using Microsoft.EntityFrameworkCore;
using NeuroGateway.Models;

namespace NeuroGateway.Repository;

/// <summary>
/// Data access for the personality table (thin 1:1 anchor per person),
/// the analyzed_data table, and the child biochemical profile tables.
/// </summary>
public class PersonalityRepository(IDbContextFactory<PersonalityDbContext> factory, PersonRepository personRepo)
{
    // ===== Personality Reads =====

    /// <summary>
    /// Get full personality for a person: communication style + all analyzed entries with biochemical profiles.
    /// Each AnalyzedEntry groups NT/hormone/peptide profiles that share the same analyzed_data_id.
    /// </summary>
    public async Task<PersonalityResult> GetPersonalityAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        // Get personality anchor
        var personality = await ctx.Personalities
            .Include(p => p.Person)
            .Where(p => p.Person.FirstName.ToLower() == person.ToLower())
            .Select(p => new { p.Person.FirstName, p.CommunicationStyle, p.Id })
            .FirstOrDefaultAsync();

        if (personality == null)
        {
            var suggestions = await personRepo.FindSimilarPersonsAsync(person);
            return new PersonalityResult(null, suggestions.Count > 0 ? suggestions : null);
        }

        // Get all profile rows with analyzed_data content via UNION ALL across 3 layers
        var rows = await ctx.Database.SqlQueryRaw<ProfileRow>("""
            SELECT ad.id AS analyzed_data_id, ad.content, ad.source_type,
                   'neurotransmitter' AS layer, nt.name AS chemical
            FROM neurotransmitter_profile np
            JOIN personality per ON per.id = np.personality_id
            LEFT JOIN analyzed_data ad ON ad.id = np.analyzed_data_id
            JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
            WHERE per.id = @p0
            UNION ALL
            SELECT ad.id, ad.content, ad.source_type, 'hormone', h.name
            FROM hormone_profile hp
            JOIN personality per ON per.id = hp.personality_id
            LEFT JOIN analyzed_data ad ON ad.id = hp.analyzed_data_id
            JOIN hormone h ON h.id = hp.hormone_id
            WHERE per.id = @p0
            UNION ALL
            SELECT ad.id, ad.content, ad.source_type, 'peptide', p.name
            FROM peptide_profile pp
            JOIN personality per ON per.id = pp.personality_id
            LEFT JOIN analyzed_data ad ON ad.id = pp.analyzed_data_id
            JOIN peptide p ON p.id = pp.peptide_id
            WHERE per.id = @p0
        """, personality.Id).ToListAsync();

        // Group by analyzed_data_id to build entries
        var entries = rows.GroupBy(r => r.AnalyzedDataId)
            .Select(g =>
            {
                var first = g.First();
                return new AnalyzedEntry(
                    first.Content ?? "unknown",
                    first.SourceType,
                    g.Where(r => r.Layer == "neurotransmitter").Select(r => r.Chemical).Distinct().ToList() is { Count: > 0 } nts ? nts : null,
                    g.Where(r => r.Layer == "hormone").Select(r => r.Chemical).Distinct().ToList() is { Count: > 0 } hs ? hs : null,
                    g.Where(r => r.Layer == "peptide").Select(r => r.Chemical).Distinct().ToList() is { Count: > 0 } ps ? ps : null,
                    first.AnalyzedDataId
                );
            }).ToList();

        return new PersonalityResult(new PersonalityProfile(personality.FirstName, personality.CommunicationStyle, entries));
    }

    // ===== Personality Anchor Management =====

    /// <summary>
    /// Ensure a personality row exists for a person (thin 1:1 anchor). Returns personality.id.
    /// Creates if missing, returns existing if already present.
    /// </summary>
    public async Task<int> EnsurePersonalityExistsAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        // Try INSERT ... ON CONFLICT DO NOTHING RETURNING id
        var ids = await ctx.Database.SqlQueryRaw<int>("""
            INSERT INTO personality (person_id)
            SELECT p.id FROM person p WHERE LOWER(p.first_name) = LOWER(@p0)
            ON CONFLICT (person_id) DO NOTHING
            RETURNING id AS "Value"
        """, person).ToListAsync();

        if (ids.Count > 0)
            return ids[0];

        // Conflict path: row already exists, fetch it
        var existingIds = await ctx.Database.SqlQueryRaw<int>("""
            SELECT per.id AS "Value"
            FROM personality per
            JOIN person p ON p.id = per.person_id
            WHERE LOWER(p.first_name) = LOWER(@p0)
        """, person).ToListAsync();

        return existingIds.FirstOrDefault();
    }

    /// <summary>
    /// Get just the communication style for a person (lightweight, no profile loading).
    /// </summary>
    public async Task<string?> GetCommunicationStyleAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Personalities
            .Include(p => p.Person)
            .Where(p => p.Person.FirstName.ToLower() == person.ToLower())
            .Select(p => p.CommunicationStyle)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Update the communication style summary for a person's personality.
    /// </summary>
    public async Task UpdateCommunicationStyleAsync(string person, string style)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE personality SET communication_style = {style}, updated_at = NOW()
            WHERE person_id = (SELECT id FROM person WHERE LOWER(first_name) = LOWER({person}))
        """);
    }

    // ===== Biochemical Profile Inserts =====

    /// <summary>
    /// Insert a neurotransmitter profile row for a personality + analyzed_data pair.
    /// Each analyzed input creates fresh profile rows (no upsert).
    /// </summary>
    public async Task InsertNeurotransmitterProfileAsync(
        int personalityId, string ntName, string reasoning, int? analyzedDataId,
        string? reasoningEmbedding = null, int? clusterId = null, bool isClusterRepresentative = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        if (reasoningEmbedding != null)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO neurotransmitter_profile (personality_id, neurotransmitter_id, reasoning, analyzed_data_id, reasoning_embedding, cluster_id, is_cluster_representative)
                SELECT {personalityId}, nt.id, {reasoning}, {analyzedDataId}, {reasoningEmbedding}::vector, {clusterId}, {isClusterRepresentative}
                FROM neurotransmitter nt WHERE nt.name = {ntName}
            """);
        }
        else
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO neurotransmitter_profile (personality_id, neurotransmitter_id, reasoning, analyzed_data_id)
                SELECT {personalityId}, nt.id, {reasoning}, {analyzedDataId}
                FROM neurotransmitter nt WHERE nt.name = {ntName}
            """);
        }
    }

    /// <summary>
    /// Insert a hormone profile row for a personality + analyzed_data pair.
    /// Each analyzed input creates fresh profile rows (no upsert).
    /// </summary>
    public async Task InsertHormoneProfileAsync(
        int personalityId, string hormoneName, string reasoning, int? analyzedDataId,
        string? reasoningEmbedding = null, int? clusterId = null, bool isClusterRepresentative = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        if (reasoningEmbedding != null)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hormone_profile (personality_id, hormone_id, reasoning, analyzed_data_id, reasoning_embedding, cluster_id, is_cluster_representative)
                SELECT {personalityId}, h.id, {reasoning}, {analyzedDataId}, {reasoningEmbedding}::vector, {clusterId}, {isClusterRepresentative}
                FROM hormone h WHERE h.name = {hormoneName}
            """);
        }
        else
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hormone_profile (personality_id, hormone_id, reasoning, analyzed_data_id)
                SELECT {personalityId}, h.id, {reasoning}, {analyzedDataId}
                FROM hormone h WHERE h.name = {hormoneName}
            """);
        }
    }

    /// <summary>
    /// Insert a peptide profile row for a personality + analyzed_data pair.
    /// Each analyzed input creates fresh profile rows (no upsert).
    /// </summary>
    public async Task InsertPeptideProfileAsync(
        int personalityId, string peptideName, string reasoning, int? analyzedDataId,
        string? reasoningEmbedding = null, int? clusterId = null, bool isClusterRepresentative = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        if (reasoningEmbedding != null)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO peptide_profile (personality_id, peptide_id, reasoning, analyzed_data_id, reasoning_embedding, cluster_id, is_cluster_representative)
                SELECT {personalityId}, pe.id, {reasoning}, {analyzedDataId}, {reasoningEmbedding}::vector, {clusterId}, {isClusterRepresentative}
                FROM peptide pe WHERE pe.name = {peptideName}
            """);
        }
        else
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO peptide_profile (personality_id, peptide_id, reasoning, analyzed_data_id)
                SELECT {personalityId}, pe.id, {reasoning}, {analyzedDataId}
                FROM peptide pe WHERE pe.name = {peptideName}
            """);
        }
    }

    // ===== Co-Occurrence Queries =====

    /// <summary>
    /// Get co-occurrences: given a source chemical from one layer, find which chemicals
    /// from a target layer appear on the same personality AND same analyzed_data_id.
    /// sourceTable/targetTable must be "neurotransmitter", "hormone", or "peptide".
    /// </summary>
    public async Task<List<CoOccurrence>> GetCoOccurrencesAsync(
        string person, string sourceTable, string sourceName, string targetTable)
    {
        var validTables = new[] { "neurotransmitter", "hormone", "peptide" };
        if (!validTables.Contains(sourceTable) || !validTables.Contains(targetTable))
            throw new ArgumentException("Tables must be 'neurotransmitter', 'hormone', or 'peptide'");
        if (sourceTable == targetTable)
            throw new ArgumentException("Source and target layers must differ");

        await using var ctx = await factory.CreateDbContextAsync();

        // Build SQL dynamically -- table names are from a fixed whitelist, safe from injection
        var sourceProfile = $"{sourceTable}_profile";
        var targetProfile = $"{targetTable}_profile";
        var sourceIdCol = $"{sourceTable}_id";
        var targetIdCol = $"{targetTable}_id";

#pragma warning disable EF1002 // Table names validated above
        var rows = await ctx.Database.SqlQueryRaw<CoOccurrenceRow>($"""
            SELECT t.name AS chemical, COUNT(*)::INT AS shared_trait_count,
                   STRING_AGG(DISTINCT ad.content, '|' ORDER BY ad.content) AS example_traits
            FROM {sourceProfile} sp
            JOIN personality per ON per.id = sp.personality_id
            JOIN person pr ON pr.id = per.person_id
            JOIN {sourceTable} s ON s.id = sp.{sourceIdCol}
            JOIN {targetProfile} tp ON tp.personality_id = sp.personality_id
                AND tp.analyzed_data_id IS NOT DISTINCT FROM sp.analyzed_data_id
            JOIN {targetTable} t ON t.id = tp.{targetIdCol}
            LEFT JOIN analyzed_data ad ON ad.id = sp.analyzed_data_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
              AND s.name = @p1
            GROUP BY t.name
            ORDER BY COUNT(*) DESC
        """, person, sourceName).ToListAsync();
#pragma warning restore EF1002

        return rows.Select(r => new CoOccurrence(
            r.Chemical,
            r.SharedTraitCount,
            r.ExampleTraits?.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList() ?? []
        )).ToList();
    }
}

/// <summary>
/// Internal DTO for SqlQueryRaw mapping of profile query results (UNION ALL across 3 layers).
/// </summary>
internal class ProfileRow
{
    public int? AnalyzedDataId { get; set; }
    public string? Content { get; set; }
    public string? SourceType { get; set; }
    public string Layer { get; set; } = "";
    public string Chemical { get; set; } = "";
}

/// <summary>
/// Internal DTO for SqlQueryRaw mapping of co-occurrence query results.
/// </summary>
internal class CoOccurrenceRow
{
    public string Chemical { get; set; } = "";
    public int SharedTraitCount { get; set; }
    public string? ExampleTraits { get; set; }
}
