using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

/// <summary>
/// Data access for hormone and peptide embedding operations —
/// vector retrieval, backfill detection, and embedding updates.
/// Trait/analyzed-data embeddings are handled by AnalyzedDataRepository.
/// </summary>
public class EmbeddingRepository(IDbContextFactory<PersonalityDbContext> factory)
{
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

    /// <summary>
    /// Update embedding vector for a hormone or peptide by id.
    /// </summary>
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
