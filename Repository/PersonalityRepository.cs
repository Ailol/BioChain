using Microsoft.EntityFrameworkCore;
using Models;

namespace Repository;

/// <summary>
/// Data access for the personality table and its child profile tables.
/// Handles trait CRUD and biochemical profile upserts/reads.
/// </summary>
public class PersonalityRepository(IDbContextFactory<PersonalityDbContext> factory, PersonRepository personRepo)
{
    // ===== Personality Trait Reads =====

    /// <summary>
    /// Get full personality for a person. Dominant NT per trait is the first linked NT
    /// (presence-based — all linked NTs are equally relevant).
    /// </summary>
    public async Task<PersonalityResult> GetPersonalityAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Personalities
            .Include(p => p.Person)
            .Include(p => p.NeurotransmitterProfiles)
                .ThenInclude(np => np.Neurotransmitter)
            .Where(p => p.Person.FirstName.ToLower() == person.ToLower())
            .OrderBy(p => p.Topic)
            .Select(p => new
            {
                p.Topic,
                p.Explanation,
                DominantNt = p.NeurotransmitterProfiles
                    .OrderBy(np => np.NeurotransmitterId)
                    .Select(np => np.Neurotransmitter.Name)
                    .FirstOrDefault(),
                p.Person.FirstName
            })
            .ToListAsync();

        if (rows.Count > 0)
        {
            var traits = rows.Select(r => new Trait(r.Topic, r.Explanation ?? "", r.DominantNt)).ToList();
            return new PersonalityResult(new PersonalityProfile(rows[0].FirstName, traits));
        }

        var suggestions = await personRepo.FindSimilarPersonsAsync(person);
        return new PersonalityResult(null, suggestions.Count > 0 ? suggestions : null);
    }

    // ===== Personality Trait Writes =====

    /// <summary>
    /// Insert a new personality row (always creates a new entry). Returns personality.id.
    /// </summary>
    public async Task<int> AddPersonalityTraitAsync(string person, string topic, string explanation)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var ids = await ctx.Database.SqlQueryRaw<int>("""
            INSERT INTO personality (person_id, topic, explanation)
            SELECT p.id, @p0, @p1
            FROM person p WHERE LOWER(p.first_name) = LOWER(@p2)
            RETURNING id AS "Value"
        """, topic, explanation, person).ToListAsync();
        return ids.FirstOrDefault();
    }

    /// <summary>
    /// Upsert a personality row for (person, topic). Returns the personality.id via RETURNING.
    /// No neurotransmitter_id — biochemical profiles are written separately.
    /// </summary>
    public async Task<int> UpsertPersonalityTraitAsync(
        string person, string topic, string explanation, string? embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        if (embeddingVector != null)
        {
            var ids = await ctx.Database.SqlQueryRaw<int>("""
                INSERT INTO personality (person_id, topic, explanation, embedding)
                SELECT p.id, @p0, @p1, @p2::vector
                FROM person p WHERE LOWER(p.first_name) = LOWER(@p3)
                ON CONFLICT (person_id, topic)
                DO UPDATE SET explanation = EXCLUDED.explanation, embedding = EXCLUDED.embedding, updated_at = NOW()
                RETURNING id AS "Value"
            """, topic, explanation, embeddingVector, person).ToListAsync();
            return ids.FirstOrDefault();
        }
        else
        {
            var ids = await ctx.Database.SqlQueryRaw<int>("""
                INSERT INTO personality (person_id, topic, explanation)
                SELECT p.id, @p0, @p1
                FROM person p WHERE LOWER(p.first_name) = LOWER(@p2)
                ON CONFLICT (person_id, topic)
                DO UPDATE SET explanation = EXCLUDED.explanation, updated_at = NOW()
                RETURNING id AS "Value"
            """, topic, explanation, person).ToListAsync();
            return ids.FirstOrDefault();
        }
    }

    // ===== Biochemical Profile Upserts =====

    /// <summary>
    /// Upsert a neurotransmitter profile row for a personality (presence-based, no strength).
    /// Supports optional clustering fields for write-time deduplication.
    /// </summary>
    public async Task UpsertNeurotransmitterProfileAsync(
        int personalityId, string ntName, string reasoning,
        string? reasoningEmbedding = null, int? clusterId = null, bool isClusterRepresentative = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        if (reasoningEmbedding != null)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO neurotransmitter_profile (personality_id, neurotransmitter_id, reasoning, reasoning_embedding, cluster_id, is_cluster_representative)
                SELECT {personalityId}, nt.id, {reasoning}, {reasoningEmbedding}::vector, {clusterId}, {isClusterRepresentative}
                FROM neurotransmitter nt WHERE nt.name = {ntName}
                ON CONFLICT (personality_id, neurotransmitter_id)
                DO UPDATE SET reasoning = EXCLUDED.reasoning, reasoning_embedding = EXCLUDED.reasoning_embedding,
                             cluster_id = EXCLUDED.cluster_id, is_cluster_representative = EXCLUDED.is_cluster_representative, updated_at = NOW()
            """);
        }
        else
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO neurotransmitter_profile (personality_id, neurotransmitter_id, reasoning)
                SELECT {personalityId}, nt.id, {reasoning}
                FROM neurotransmitter nt WHERE nt.name = {ntName}
                ON CONFLICT (personality_id, neurotransmitter_id)
                DO UPDATE SET reasoning = EXCLUDED.reasoning, updated_at = NOW()
            """);
        }
    }

    /// <summary>
    /// Upsert a hormone profile row for a personality (presence-based, no strength).
    /// Supports optional clustering fields for write-time deduplication.
    /// </summary>
    public async Task UpsertHormoneProfileAsync(
        int personalityId, string hormoneName, string reasoning,
        string? reasoningEmbedding = null, int? clusterId = null, bool isClusterRepresentative = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        if (reasoningEmbedding != null)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hormone_profile (personality_id, hormone_id, reasoning, reasoning_embedding, cluster_id, is_cluster_representative)
                SELECT {personalityId}, h.id, {reasoning}, {reasoningEmbedding}::vector, {clusterId}, {isClusterRepresentative}
                FROM hormone h WHERE h.name = {hormoneName}
                ON CONFLICT (personality_id, hormone_id)
                DO UPDATE SET reasoning = EXCLUDED.reasoning, reasoning_embedding = EXCLUDED.reasoning_embedding,
                             cluster_id = EXCLUDED.cluster_id, is_cluster_representative = EXCLUDED.is_cluster_representative, updated_at = NOW()
            """);
        }
        else
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hormone_profile (personality_id, hormone_id, reasoning)
                SELECT {personalityId}, h.id, {reasoning}
                FROM hormone h WHERE h.name = {hormoneName}
                ON CONFLICT (personality_id, hormone_id)
                DO UPDATE SET reasoning = EXCLUDED.reasoning, updated_at = NOW()
            """);
        }
    }

    /// <summary>
    /// Upsert a peptide profile row for a personality (presence-based, no strength).
    /// Supports optional clustering fields for write-time deduplication.
    /// </summary>
    public async Task UpsertPeptideProfileAsync(
        int personalityId, string peptideName, string reasoning,
        string? reasoningEmbedding = null, int? clusterId = null, bool isClusterRepresentative = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        if (reasoningEmbedding != null)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO peptide_profile (personality_id, peptide_id, reasoning, reasoning_embedding, cluster_id, is_cluster_representative)
                SELECT {personalityId}, pe.id, {reasoning}, {reasoningEmbedding}::vector, {clusterId}, {isClusterRepresentative}
                FROM peptide pe WHERE pe.name = {peptideName}
                ON CONFLICT (personality_id, peptide_id)
                DO UPDATE SET reasoning = EXCLUDED.reasoning, reasoning_embedding = EXCLUDED.reasoning_embedding,
                             cluster_id = EXCLUDED.cluster_id, is_cluster_representative = EXCLUDED.is_cluster_representative, updated_at = NOW()
            """);
        }
        else
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO peptide_profile (personality_id, peptide_id, reasoning)
                SELECT {personalityId}, pe.id, {reasoning}
                FROM peptide pe WHERE pe.name = {peptideName}
                ON CONFLICT (personality_id, peptide_id)
                DO UPDATE SET reasoning = EXCLUDED.reasoning, updated_at = NOW()
            """);
        }
    }

    // ===== Clustering Methods =====

    /// <summary>
    /// Find the nearest cluster in a profile table for a given person and embedding vector.
    /// Returns (ClusterId, Distance) of the nearest cluster representative, or null if none.
    /// profileTable must be "neurotransmitter_profile", "hormone_profile", or "peptide_profile".
    /// </summary>
    public async Task<(int ClusterId, double Distance)?> FindNearestClusterAsync(
        string profileTable, string person, string embeddingVector)
    {
        var validTables = new[] { "neurotransmitter_profile", "hormone_profile", "peptide_profile" };
        if (!validTables.Contains(profileTable))
            throw new ArgumentException("Invalid profile table name");

        await using var ctx = await factory.CreateDbContextAsync();

#pragma warning disable EF1002 // Table names validated above
        var rows = await ctx.Database.SqlQueryRaw<ClusterDistanceRow>($"""
            SELECT pt.cluster_id AS cluster_id, (pt.reasoning_embedding <=> @p0::vector) AS distance
            FROM {profileTable} pt
            JOIN personality pe ON pe.id = pt.personality_id
            JOIN person pr ON pr.id = pe.person_id
            WHERE LOWER(pr.first_name) = LOWER(@p1)
              AND pt.is_cluster_representative = true
              AND pt.reasoning_embedding IS NOT NULL
            ORDER BY pt.reasoning_embedding <=> @p0::vector
            LIMIT 1
        """, embeddingVector, person).ToListAsync();
#pragma warning restore EF1002

        if (rows.Count == 0 || rows[0].ClusterId == null)
            return null;

        return (rows[0].ClusterId!.Value, rows[0].Distance);
    }

    /// <summary>
    /// Get the next available cluster ID for a person in a profile table.
    /// </summary>
    public async Task<int> GetNextClusterIdAsync(string profileTable, string person)
    {
        var validTables = new[] { "neurotransmitter_profile", "hormone_profile", "peptide_profile" };
        if (!validTables.Contains(profileTable))
            throw new ArgumentException("Invalid profile table name");

        await using var ctx = await factory.CreateDbContextAsync();

#pragma warning disable EF1002 // Table names validated above
        var rows = await ctx.Database.SqlQueryRaw<int>($"""
            SELECT COALESCE(MAX(pt.cluster_id), 0) + 1 AS "Value"
            FROM {profileTable} pt
            JOIN personality pe ON pe.id = pt.personality_id
            JOIN person pr ON pr.id = pe.person_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
        """, person).ToListAsync();
#pragma warning restore EF1002

        return rows.FirstOrDefault(1);
    }

    /// <summary>
    /// Get deduplicated profiles for a person — only cluster representatives.
    /// Returns a dictionary with keys "neurotransmitter", "hormone", "peptide",
    /// each containing a list of "ChemicalName: reasoning..." strings.
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetDeduplicatedProfilesAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var ntProfiles = await ctx.Database.SqlQueryRaw<ChemicalReasoningRow>("""
            SELECT nt.name AS chemical, np.reasoning AS reasoning
            FROM neurotransmitter_profile np
            JOIN personality pe ON pe.id = np.personality_id
            JOIN person pr ON pr.id = pe.person_id
            JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
              AND np.is_cluster_representative = true
            ORDER BY nt.name
        """, person).ToListAsync();

        var hormoneProfiles = await ctx.Database.SqlQueryRaw<ChemicalReasoningRow>("""
            SELECT h.name AS chemical, hp.reasoning AS reasoning
            FROM hormone_profile hp
            JOIN personality pe ON pe.id = hp.personality_id
            JOIN person pr ON pr.id = pe.person_id
            JOIN hormone h ON h.id = hp.hormone_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
              AND hp.is_cluster_representative = true
            ORDER BY h.name
        """, person).ToListAsync();

        var peptideProfiles = await ctx.Database.SqlQueryRaw<ChemicalReasoningRow>("""
            SELECT p.name AS chemical, pp.reasoning AS reasoning
            FROM peptide_profile pp
            JOIN personality pe ON pe.id = pp.personality_id
            JOIN person pr ON pr.id = pe.person_id
            JOIN peptide p ON p.id = pp.peptide_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
              AND pp.is_cluster_representative = true
            ORDER BY p.name
        """, person).ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["neurotransmitter"] = ntProfiles.Select(r => $"{r.Chemical}: {r.Reasoning}").ToList(),
            ["hormone"] = hormoneProfiles.Select(r => $"{r.Chemical}: {r.Reasoning}").ToList(),
            ["peptide"] = peptideProfiles.Select(r => $"{r.Chemical}: {r.Reasoning}").ToList()
        };
    }

    // ===== Biochemical Profile Reads (count-based) =====

    /// <summary>
    /// Get hormone trait counts for a person — GROUP BY hormone, COUNT entries.
    /// </summary>
    public async Task<List<ChemicalScore>> GetHormoneScoresAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Database.SqlQueryRaw<InteractionRow>("""
            SELECT h.name AS name, COUNT(*)::INT AS trait_count
            FROM hormone_profile hp
            JOIN personality p ON p.id = hp.personality_id
            JOIN person pr ON pr.id = p.person_id
            JOIN hormone h ON h.id = hp.hormone_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
            GROUP BY h.name
            ORDER BY COUNT(*) DESC
        """, person).ToListAsync();

        return rows.Select(r => new ChemicalScore(r.Name, r.TraitCount)).ToList();
    }

    public async Task<List<ChemicalScore>> GetPeptideScoresAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Database.SqlQueryRaw<InteractionRow>("""
            SELECT pe.name AS name, COUNT(*)::INT AS trait_count
            FROM peptide_profile pp
            JOIN personality p ON p.id = pp.personality_id
            JOIN person pr ON pr.id = p.person_id
            JOIN peptide pe ON pe.id = pp.peptide_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
            GROUP BY pe.name
            ORDER BY COUNT(*) DESC
        """, person).ToListAsync();

        return rows.Select(r => new ChemicalScore(r.Name, r.TraitCount)).ToList();
    }

    // ===== Co-Occurrence Queries =====

    /// <summary>
    /// Get co-occurrences: given a source chemical from one layer, find which chemicals
    /// from a target layer appear on the same personality traits.
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

        // Build SQL dynamically — table names are from a fixed whitelist, safe from injection
        var sourceProfile = $"{sourceTable}_profile";
        var targetProfile = $"{targetTable}_profile";
        var sourceIdCol = $"{sourceTable}_id";
        var targetIdCol = $"{targetTable}_id";

#pragma warning disable EF1002 // Table names validated above
        var rows = await ctx.Database.SqlQueryRaw<CoOccurrenceRow>($"""
            SELECT t.name AS chemical, COUNT(*)::INT AS shared_trait_count,
                   STRING_AGG(DISTINCT per.topic, '|' ORDER BY per.topic) AS example_traits
            FROM {sourceProfile} sp
            JOIN personality per ON per.id = sp.personality_id
            JOIN person pr ON pr.id = per.person_id
            JOIN {sourceTable} s ON s.id = sp.{sourceIdCol}
            JOIN {targetProfile} tp ON tp.personality_id = sp.personality_id
            JOIN {targetTable} t ON t.id = tp.{targetIdCol}
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
/// Internal DTO for SqlQueryRaw mapping of count-based interaction scores.
/// </summary>
internal class InteractionRow
{
    public string Name { get; set; } = "";
    public int TraitCount { get; set; }
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

/// <summary>
/// Internal DTO for cluster distance query results.
/// </summary>
internal class ClusterDistanceRow
{
    public int? ClusterId { get; set; }
    public double Distance { get; set; }
}

/// <summary>
/// Internal DTO for deduplicated chemical+reasoning query results.
/// </summary>
internal class ChemicalReasoningRow
{
    public string Chemical { get; set; } = "";
    public string? Reasoning { get; set; }
}
