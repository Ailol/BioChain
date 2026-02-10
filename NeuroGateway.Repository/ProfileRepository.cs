using Microsoft.EntityFrameworkCore;
using NeuroGateway.Models;

namespace NeuroGateway.Repository;

/// <summary>
/// Data access for biochemical profile reads: clustering, deduplication, and count-based scores.
/// Extracted from PersonalityRepository for single-responsibility.
/// </summary>
public class ProfileRepository(IDbContextFactory<PersonalityDbContext> factory)
{
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

    // ===== Full Biochemical Profile (similarity-ranked via SQL function) =====

    /// <summary>
    /// Get the full biochemical profile for a person, scored by reasoning_embedding similarity to the input.
    /// Calls the get_full_biochemical_profile SQL function which UNIONs all 3 layers.
    /// Returns dict with keys "neurotransmitter", "hormone", "peptide" — each a list of "Chemical: reasoning" strings.
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetFullBiochemicalProfileAsync(string person, string? embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        List<BiochemicalProfileRow> rows;
        if (embeddingVector != null)
        {
            rows = await ctx.Database.SqlQueryRaw<BiochemicalProfileRow>("""
                SELECT layer, chemical_name, reasoning, analyzed_data_id, similarity
                FROM get_full_biochemical_profile(
                    (SELECT pr.id FROM person pr WHERE LOWER(pr.first_name) = LOWER(@p0)),
                    @p1::vector
                )
            """, person, embeddingVector).ToListAsync();
        }
        else
        {
            rows = await ctx.Database.SqlQueryRaw<BiochemicalProfileRow>("""
                SELECT layer, chemical_name, reasoning, analyzed_data_id, similarity
                FROM get_full_biochemical_profile(
                    (SELECT pr.id FROM person pr WHERE LOWER(pr.first_name) = LOWER(@p0))
                )
            """, person).ToListAsync();
        }

        var result = new Dictionary<string, List<string>>
        {
            ["neurotransmitter"] = [],
            ["hormone"] = [],
            ["peptide"] = []
        };

        foreach (var row in rows)
        {
            if (result.TryGetValue(row.Layer, out var list))
                list.Add($"{row.ChemicalName}: {row.Reasoning}");
        }

        return result;
    }

    // ===== Per-Layer Queries (for ProfileScoringService) =====

    /// <summary>
    /// Get reasoning_embedding vectors (as PostgreSQL text) for cluster representatives in a single layer.
    /// Used for computing per-layer centroids for relationship estimation.
    /// Caller parses via VectorAlgorithms.ParsePostgresVector.
    /// </summary>
    public async Task<List<string>> GetLayerEmbeddingTextsAsync(string person, string profileTable)
    {
        var validTables = new[] { "neurotransmitter_profile", "hormone_profile", "peptide_profile" };
        if (!validTables.Contains(profileTable))
            throw new ArgumentException("Invalid profile table name");

        await using var ctx = await factory.CreateDbContextAsync();

#pragma warning disable EF1002 // Table names validated above
        var rows = await ctx.Database.SqlQueryRaw<EmbeddingTextRow>($"""
            SELECT pt.reasoning_embedding::text AS embedding_text
            FROM {profileTable} pt
            JOIN personality pe ON pe.id = pt.personality_id
            JOIN person pr ON pr.id = pe.person_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
              AND pt.is_cluster_representative = true
              AND pt.reasoning_embedding IS NOT NULL
        """, person).ToListAsync();
#pragma warning restore EF1002

        return rows
            .Where(r => !string.IsNullOrEmpty(r.EmbeddingText))
            .Select(r => r.EmbeddingText!)
            .ToList();
    }

    /// <summary>
    /// Fallback: unranked reasoning when no embedding available for scoring.
    /// Returns cluster representatives only.
    /// </summary>
    public async Task<List<string>> GetUnrankedLayerProfileAsync(
        string person, string profileTable, string chemicalTable, string chemicalFk)
    {
        var validProfileTables = new[] { "neurotransmitter_profile", "hormone_profile", "peptide_profile" };
        var validChemicalTables = new[] { "neurotransmitter", "hormone", "peptide" };
        if (!validProfileTables.Contains(profileTable) || !validChemicalTables.Contains(chemicalTable))
            throw new ArgumentException("Invalid table name");

        await using var ctx = await factory.CreateDbContextAsync();

#pragma warning disable EF1002 // Table names validated above
        var rows = await ctx.Database.SqlQueryRaw<ChemicalReasoningRow>($"""
            SELECT c.name AS chemical, p.reasoning AS reasoning
            FROM {profileTable} p
            JOIN personality pe ON pe.id = p.personality_id
            JOIN person pr ON pr.id = pe.person_id
            JOIN {chemicalTable} c ON c.id = p.{chemicalFk}
            WHERE LOWER(pr.first_name) = LOWER(@p0)
              AND p.is_cluster_representative = true
            ORDER BY c.name
        """, person).ToListAsync();
#pragma warning restore EF1002

        return rows.Select(r => $"{r.Chemical}: {r.Reasoning}").ToList();
    }

    /// <summary>
    /// Dual-vector scoring via get_scored_layer_profile SQL function.
    /// Scores against BOTH message and relationship embeddings separately.
    /// Per-chemical coverage guarantee + temporal freshness boost.
    /// Returns formatted "Chemical (score): reasoning" strings ordered by freshness_score.
    /// </summary>
    public async Task<List<string>> GetDualScoredLayerProfileAsync(
        string person, string layer, string messageEmbeddingVector, string relationshipEmbeddingVector,
        float messageWeight = 0.6f, int topPerChemical = 1)
    {
        var validLayers = new[] { "neurotransmitter", "hormone", "peptide" };
        if (!validLayers.Contains(layer))
            throw new ArgumentException("Invalid layer name");

        await using var ctx = await factory.CreateDbContextAsync();

        var rows = await ctx.Database.SqlQueryRaw<DualScoredReasoningRow>(
            "SELECT * FROM get_scored_layer_profile(@p0, @p1, @p2::vector, @p3::vector, @p4, @p5)",
            person, layer, messageEmbeddingVector, relationshipEmbeddingVector,
            (double)messageWeight, topPerChemical).ToListAsync();

        return rows.Select(r => $"{r.ChemicalName} ({r.FreshnessScore:F2}): {r.Reasoning}").ToList();
    }

    // ===== Biochemical Profile Reads (count-based) =====

    /// <summary>
    /// Get neurotransmitter trait counts for a person — GROUP BY neurotransmitter, COUNT entries.
    /// </summary>
    public async Task<List<ChemicalScore>> GetNeurotransmitterScoresAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Database.SqlQueryRaw<InteractionRow>("""
            SELECT nt.name AS name, COUNT(*)::INT AS trait_count
            FROM neurotransmitter_profile np
            JOIN personality p ON p.id = np.personality_id
            JOIN person pr ON pr.id = p.person_id
            JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
            WHERE LOWER(pr.first_name) = LOWER(@p0)
            GROUP BY nt.name
            ORDER BY COUNT(*) DESC
        """, person).ToListAsync();

        return rows.Select(r => new ChemicalScore(r.Name, r.TraitCount)).ToList();
    }

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

/// <summary>
/// Internal DTO for get_full_biochemical_profile SQL function results.
/// </summary>
internal class BiochemicalProfileRow
{
    public string Layer { get; set; } = "";
    public string ChemicalName { get; set; } = "";
    public string? Reasoning { get; set; }
    public int? AnalyzedDataId { get; set; }
    public double Similarity { get; set; }
}

/// <summary>
/// Internal DTO for extracting embedding text from SQL (vector cast to text).
/// </summary>
internal class EmbeddingTextRow
{
    public string? EmbeddingText { get; set; }
}

/// <summary>
/// Internal DTO for get_scored_layer_profile SQL function results (dual-vector scoring).
/// </summary>
internal class DualScoredReasoningRow
{
    public string ChemicalName { get; set; } = "";
    public string? Reasoning { get; set; }
    public int? AnalyzedDataId { get; set; }
    public double MessageSim { get; set; }
    public double RelationshipSim { get; set; }
    public double CompositeScore { get; set; }
    public double FreshnessScore { get; set; }
}
