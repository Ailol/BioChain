using Microsoft.EntityFrameworkCore;
using Models;

namespace Repository;

/// <summary>
/// Consolidated data access for all embedding operations — trait embeddings,
/// hormone/peptide embeddings, vector similarity search, and backfill.
/// </summary>
public class EmbeddingRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    // ===== Trait Embeddings =====

    /// <summary>
    /// Get raw trait embedding vectors for a person (for computing hormone/peptide scores).
    /// </summary>
    public async Task<List<float[]>> GetTraitEmbeddingsAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entities = await ctx.Personalities
            .Include(p => p.Person)
            .Where(p => p.Person.FirstName.ToLower() == person.ToLower() && p.Embedding != null)
            .Select(p => p.Embedding!)
            .ToListAsync();

        return entities.Select(e => VectorMath.ToFloatArray(e)!).ToList();
    }

    /// <summary>
    /// Get trait embeddings with full metadata (topic, explanation, dominant NT, embedding) for vector analysis.
    /// Dominant NT is the first linked NT (presence-based — all linked NTs are equally relevant).
    /// </summary>
    public async Task<List<TraitWithEmbedding>> GetTraitEmbeddingsWithMetadataAsync(string person)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var entities = await ctx.Personalities
            .Include(p => p.Person)
            .Include(p => p.NeurotransmitterProfiles)
                .ThenInclude(np => np.Neurotransmitter)
            .Where(p => p.Person.FirstName.ToLower() == person.ToLower() && p.Embedding != null)
            .Select(p => new
            {
                p.Topic,
                p.Explanation,
                DominantNt = p.NeurotransmitterProfiles
                    .OrderBy(np => np.NeurotransmitterId)
                    .Select(np => np.Neurotransmitter.Name)
                    .FirstOrDefault() ?? "Unknown",
                p.Embedding
            })
            .ToListAsync();

        return entities
            .Where(e => e.Embedding != null)
            .Select(e => new TraitWithEmbedding(e.Topic, e.Explanation ?? "", e.DominantNt, VectorMath.ToFloatArray(e.Embedding!)!))
            .ToList();
    }

    /// <summary>
    /// Search personality traits by vector similarity (pgvector cosine distance).
    /// Returns trait data with similarity scores. NT is the first linked profile (presence-based).
    /// </summary>
    public async Task<List<(string Topic, string Explanation, string Neurotransmitter, double Similarity)>> GetSimilarTraitsAsync(
        string person, string embeddingVector, int limit = 20)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var rows = await ctx.Database.SqlQueryRaw<SimilarTraitRow>("""
            SELECT p.topic AS "Topic", p.explanation AS "Explanation",
                   COALESCE(
                       (SELECT nt.name FROM neurotransmitter_profile np
                        JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
                        WHERE np.personality_id = p.id
                        ORDER BY np.neurotransmitter_id LIMIT 1),
                       'Unknown'
                   ) AS "Neurotransmitter",
                   1 - (p.embedding <=> @p0::vector) AS "Similarity"
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            WHERE LOWER(pr.first_name) = LOWER(@p1) AND p.embedding IS NOT NULL
            ORDER BY p.embedding <=> @p0::vector
            LIMIT @p2
        """, embeddingVector, person, limit).ToListAsync();

        return rows.Select(r => (r.Topic, r.Explanation ?? "", r.Neurotransmitter, r.Similarity)).ToList();
    }

    // ===== Trait Embedding Backfill =====

    public async Task<List<(int Id, string Topic, string Explanation)>> GetTraitsWithoutEmbeddingsAsync(string? person = null)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var query = ctx.Personalities
            .Include(p => p.Person)
            .Where(p => p.Embedding == null);

        if (person != null)
            query = query.Where(p => p.Person.FirstName.ToLower() == person.ToLower());

        var entities = await query
            .Select(p => new { p.Id, p.Topic, p.Explanation })
            .ToListAsync();

        return entities.Select(e => (e.Id, e.Topic, e.Explanation ?? "")).ToList();
    }

    public async Task UpdateTraitEmbeddingAsync(int id, string embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE personality SET embedding = {embeddingVector}::vector, updated_at = NOW() WHERE id = {id}
        """);
    }

    /// <summary>
    /// Update embedding for a specific trait by (person, topic).
    /// </summary>
    public async Task UpdateTraitEmbeddingByContentAsync(string person, string topic, string embeddingVector)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE personality SET embedding = {embeddingVector}::vector, updated_at = NOW()
            WHERE person_id = (SELECT id FROM person WHERE LOWER(first_name) = LOWER({person}))
              AND topic = {topic}
        """);
    }

    // ===== Hormone/Peptide Embeddings =====

    /// <summary>
    /// Get raw name + embedding pairs for hormone or peptide table (for heatmap analysis).
    /// </summary>
    public async Task<List<(string Name, float[] Embedding)>> GetTargetEmbeddingsAsync(string table)
    {
        if (table is not "hormone" and not "peptide")
            throw new ArgumentException("Table must be 'hormone' or 'peptide'", nameof(table));

        await using var ctx = await factory.CreateDbContextAsync();

        if (table == "hormone")
        {
            var entities = await ctx.Hormones
                .Where(h => h.Embedding != null)
                .Select(h => new { h.Name, h.Embedding })
                .ToListAsync();

            return entities
                .Where(e => e.Embedding != null)
                .Select(e => (e.Name, Embedding: VectorMath.ToFloatArray(e.Embedding!)!))
                .ToList();
        }
        else
        {
            var entities = await ctx.Peptides
                .Where(p => p.Embedding != null)
                .Select(p => new { p.Name, p.Embedding })
                .ToListAsync();

            return entities
                .Where(e => e.Embedding != null)
                .Select(e => (e.Name, Embedding: VectorMath.ToFloatArray(e.Embedding!)!))
                .ToList();
        }
    }

    /// <summary>
    /// Get hormones/peptides that don't have embeddings yet.
    /// </summary>
    public async Task<List<(string Table, int Id, string Name)>> GetItemsWithoutEmbeddingsAsync()
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var hormones = await ctx.Hormones
            .Where(h => h.Embedding == null)
            .Select(h => new { h.Id, h.Name })
            .ToListAsync();

        var peptides = await ctx.Peptides
            .Where(p => p.Embedding == null)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();

        var items = new List<(string Table, int Id, string Name)>();
        items.AddRange(hormones.Select(h => ("hormone", h.Id, h.Name)));
        items.AddRange(peptides.Select(p => ("peptide", p.Id, p.Name)));
        return items;
    }

    public async Task UpdateItemEmbeddingAsync(string table, int id, string embeddingVector)
    {
        if (table is not "hormone" and not "peptide")
            throw new ArgumentException("Table must be 'hormone' or 'peptide'", nameof(table));

        await using var ctx = await factory.CreateDbContextAsync();
#pragma warning disable EF1002 // Table name is validated, not user input
        await ctx.Database.ExecuteSqlRawAsync(
            $"UPDATE {table} SET embedding = @p0::vector WHERE id = @p1",
            embeddingVector, id);
#pragma warning restore EF1002
    }
}

/// <summary>
/// Internal DTO for SqlQueryRaw mapping of similar traits query.
/// </summary>
internal class SimilarTraitRow
{
    public string Topic { get; set; } = "";
    public string? Explanation { get; set; }
    public string Neurotransmitter { get; set; } = "";
    public double Similarity { get; set; }
}
