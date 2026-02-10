using Microsoft.EntityFrameworkCore;
using NeuroGateway.Models;

namespace NeuroGateway.Repository;

/// <summary>
/// Data access for the analyzed_data table.
/// Handles CRUD and vector similarity search for analyzed inputs.
/// </summary>
public class AnalyzedDataRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    // ===== Insert =====

    /// <summary>
    /// Insert a new analyzed_data row. Returns the ID.
    /// </summary>
    public async Task<int> InsertAsync(string person, string content, string? sourceType, string? sourceUri, string? embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        if (embeddingVector != null)
        {
            var ids = await ctx.Database.SqlQueryRaw<int>("""
                INSERT INTO analyzed_data (person_id, content, source_type, source_uri, embedding)
                SELECT p.id, @p0, @p1, @p2, @p3::vector
                FROM person p WHERE LOWER(p.first_name) = LOWER(@p4)
                RETURNING id AS "Value"
            """, content, sourceType ?? (object)DBNull.Value, sourceUri ?? (object)DBNull.Value, embeddingVector, person).ToListAsync();
            return ids.FirstOrDefault();
        }
        else
        {
            var ids = await ctx.Database.SqlQueryRaw<int>("""
                INSERT INTO analyzed_data (person_id, content, source_type, source_uri)
                SELECT p.id, @p0, @p1, @p2
                FROM person p WHERE LOWER(p.first_name) = LOWER(@p3)
                RETURNING id AS "Value"
            """, content, sourceType ?? (object)DBNull.Value, sourceUri ?? (object)DBNull.Value, person).ToListAsync();
            return ids.FirstOrDefault();
        }
    }

    // ===== Vector Similarity Search =====

    /// <summary>
    /// Search analyzed_data by vector similarity (pgvector cosine distance).
    /// Returns (Content, Neurotransmitter (from joined NT profiles), Similarity) tuples.
    /// </summary>
    public async Task<List<(string Content, string Neurotransmitter, double Similarity)>> GetSimilarAsync(
        string person, string embeddingVector, int limit = 20)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Database.SqlQueryRaw<SimilarAnalyzedRow>("""
            SELECT ad.content, COALESCE(nt.name, 'Unknown') AS neurotransmitter,
                   1 - (ad.embedding <=> @p0::vector) AS similarity
            FROM (
                SELECT ad2.id, ad2.person_id, ad2.content, ad2.embedding
                FROM analyzed_data ad2
                JOIN person pr ON pr.id = ad2.person_id
                WHERE LOWER(pr.first_name) = LOWER(@p1) AND ad2.embedding IS NOT NULL
                ORDER BY ad2.embedding <=> @p0::vector
                LIMIT @p2
            ) ad
            LEFT JOIN personality per ON per.person_id = ad.person_id
            LEFT JOIN neurotransmitter_profile np ON np.personality_id = per.id AND np.analyzed_data_id = ad.id
            LEFT JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
            ORDER BY ad.embedding <=> @p0::vector, nt.name
        """, embeddingVector, person, limit).ToListAsync();

        return rows.Select(r => (r.Content, r.Neurotransmitter, r.Similarity)).ToList();
    }

    /// <summary>
    /// Find the single nearest analyzed_data row for dedup checking.
    /// Returns (AnalyzedDataId, Content, Similarity) or null if no rows exist.
    /// </summary>
    public async Task<(int AnalyzedDataId, string Content, double Similarity)?> FindNearestAsync(
        string person, string embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Database.SqlQueryRaw<NearestAnalyzedRow>("""
            SELECT ad.id AS analyzed_data_id, ad.content,
                   1 - (ad.embedding <=> @p0::vector) AS similarity
            FROM analyzed_data ad
            JOIN person pr ON pr.id = ad.person_id
            WHERE LOWER(pr.first_name) = LOWER(@p1) AND ad.embedding IS NOT NULL
            ORDER BY ad.embedding <=> @p0::vector
            LIMIT 1
        """, embeddingVector, person).ToListAsync();

        if (rows.Count == 0) return null;
        return (rows[0].AnalyzedDataId, rows[0].Content, rows[0].Similarity);
    }

    // ===== Embedding Backfill =====

    /// <summary>
    /// Get analyzed_data rows that don't have embeddings yet (for backfill).
    /// Optionally filter by person name.
    /// </summary>
    public async Task<List<(int Id, string Content)>> GetWithoutEmbeddingsAsync(string? person = null)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var query = ctx.AnalyzedDataSet
            .Include(ad => ad.Person)
            .Where(ad => ad.Embedding == null);

        if (person != null)
            query = query.Where(ad => ad.Person.FirstName.ToLower() == person.ToLower());

        var entities = await query
            .Select(ad => new { ad.Id, ad.Content })
            .ToListAsync();

        return entities.Select(e => (e.Id, e.Content)).ToList();
    }

    /// <summary>
    /// Update embedding on a specific analyzed_data row.
    /// </summary>
    public async Task UpdateEmbeddingAsync(int id, string embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE analyzed_data SET embedding = {embeddingVector}::vector WHERE id = {id}
        """);
    }

    // ===== Metadata Queries =====

    /// <summary>
    /// Get analyzed_data with embeddings + NT metadata for clustering analysis.
    /// This replaces EmbeddingRepository.GetTraitEmbeddingsWithMetadataAsync.
    /// </summary>
    public async Task<List<AnalyzedDataWithEmbedding>> GetWithEmbeddingsAndMetadataAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entities = await ctx.AnalyzedDataSet
            .Include(ad => ad.Person)
            .Include(ad => ad.NeurotransmitterProfiles)
                .ThenInclude(np => np.Neurotransmitter)
            .Where(ad => ad.Person.FirstName.ToLower() == person.ToLower() && ad.Embedding != null)
            .Select(ad => new
            {
                ad.Id,
                ad.Content,
                ad.SourceType,
                Neurotransmitters = ad.NeurotransmitterProfiles
                    .OrderBy(np => np.Neurotransmitter.Name)
                    .Select(np => np.Neurotransmitter.Name)
                    .ToList(),
                ad.Embedding
            })
            .ToListAsync();

        return entities
            .Where(e => e.Embedding != null)
            .Select(e => new AnalyzedDataWithEmbedding(
                e.Content,
                e.SourceType ?? "",
                e.Neurotransmitters,
                VectorMath.ToFloatArray(e.Embedding!)!,
                e.Id))
            .ToList();
    }

    /// <summary>
    /// Get raw embedding vectors (float arrays) for a person.
    /// </summary>
    public async Task<List<float[]>> GetEmbeddingsAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entities = await ctx.AnalyzedDataSet
            .Include(ad => ad.Person)
            .Where(ad => ad.Person.FirstName.ToLower() == person.ToLower() && ad.Embedding != null)
            .Select(ad => ad.Embedding!)
            .ToListAsync();

        return entities.Select(e => VectorMath.ToFloatArray(e)!).ToList();
    }
}

/// <summary>
/// Internal DTO for SqlQueryRaw mapping of similar analyzed_data query.
/// </summary>
internal class SimilarAnalyzedRow
{
    public string Content { get; set; } = "";
    public string Neurotransmitter { get; set; } = "";
    public double Similarity { get; set; }
}

/// <summary>
/// Internal DTO for SqlQueryRaw mapping of nearest analyzed_data query.
/// </summary>
internal class NearestAnalyzedRow
{
    public int AnalyzedDataId { get; set; }
    public string Content { get; set; } = "";
    public double Similarity { get; set; }
}
