using Models;
using Repository;

namespace Agents;

/// <summary>
/// Service for generating vector embeddings and backfilling missing embeddings in the database.
/// </summary>
public class EmbeddingService
{
    private readonly LlmService _llm;
    private readonly EmbeddingRepository _embeddingRepo;

    public EmbeddingService(LlmService llm, EmbeddingRepository embeddingRepo)
    {
        _llm = llm;
        _embeddingRepo = embeddingRepo;
    }

    /// <summary>
    /// Generate an embedding vector for the given text using Ollama's embed API.
    /// </summary>
    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        return await _llm.EmbedAsync(text);
    }

    /// <summary>
    /// Generate an embedding for a personality trait by combining topic and explanation.
    /// </summary>
    public async Task<float[]?> GenerateTraitEmbeddingAsync(string topic, string explanation)
    {
        var text = $"{topic}: {explanation}";
        return await _llm.EmbedAsync(text);
    }

    /// <summary>
    /// Generate an embedding for a hormone or peptide name.
    /// </summary>
    public async Task<float[]?> GenerateNameEmbeddingAsync(string name)
    {
        return await _llm.EmbedAsync(name);
    }

    /// <summary>
    /// Calculate cosine similarity between two embedding vectors.
    /// Returns a value between -1 and 1, where 1 means identical direction.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0)
            return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Aggregate multiple embeddings into a single embedding using mean pooling.
    /// This creates a "centroid" vector representing the average of all input embeddings.
    /// </summary>
    public static float[] AggregateEmbeddings(IEnumerable<float[]> embeddings)
    {
        var embeddingList = embeddings.Where(e => e != null).ToList();

        if (embeddingList.Count == 0)
            return Array.Empty<float>();

        var dimension = embeddingList[0].Length;
        var result = new float[dimension];

        foreach (var embedding in embeddingList)
        {
            for (int i = 0; i < dimension; i++)
            {
                result[i] += embedding[i];
            }
        }

        // Normalize by count (mean pooling)
        for (int i = 0; i < dimension; i++)
        {
            result[i] /= embeddingList.Count;
        }

        return result;
    }

    /// <summary>
    /// Format a float array as a PostgreSQL vector string for insertion.
    /// </summary>
    public static string ToPostgresVector(float[] embedding)
    {
        if (embedding == null || embedding.Length == 0)
            return "NULL";

        return $"[{string.Join(",", embedding.Select(f => f.ToString("G9")))}]";
    }

    /// <summary>
    /// Parse a PostgreSQL vector string back to a float array.
    /// </summary>
    public static float[]? FromPostgresVector(string? vectorString)
    {
        if (string.IsNullOrWhiteSpace(vectorString))
            return null;

        // Remove brackets and split
        var trimmed = vectorString.Trim('[', ']', '(', ')');
        var parts = trimmed.Split(',');

        try
        {
            return parts.Select(p => float.Parse(p.Trim())).ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generate embeddings for all traits that don't have them yet.
    /// </summary>
    public async Task<BackfillResult> BackfillEmbeddingsAsync(string? person = null)
    {
        var traits = await _embeddingRepo.GetTraitsWithoutEmbeddingsAsync(person);

        int updated = 0, skipped = 0, errors = 0;

        foreach (var (id, topic, explanation) in traits)
        {
            try
            {
                var embedding = await GenerateTraitEmbeddingAsync(topic, explanation);
                if (embedding == null) { skipped++; continue; }

                var embeddingValue = ToPostgresVector(embedding);
                await _embeddingRepo.UpdateTraitEmbeddingAsync(id, embeddingValue);
                updated++;
            }
            catch { errors++; }
        }

        return new BackfillResult(updated, skipped, errors,
            $"Backfill complete: {updated} updated, {skipped} skipped, {errors} errors out of {traits.Count} traits.");
    }

    /// <summary>
    /// Generate embeddings for all hormones and peptides that don't have embeddings yet.
    /// Uses the name directly for embedding generation (no description column in new schema).
    /// </summary>
    public async Task<BackfillResult> BackfillHormonePeptideEmbeddingsAsync()
    {
        var items = await _embeddingRepo.GetItemsWithoutEmbeddingsAsync();

        int updated = 0, skipped = 0, errors = 0;

        foreach (var (table, id, name) in items)
        {
            try
            {
                var embedding = await GenerateNameEmbeddingAsync(name);
                if (embedding == null) { skipped++; continue; }

                var embeddingValue = ToPostgresVector(embedding);
                await _embeddingRepo.UpdateItemEmbeddingAsync(table, id, embeddingValue);
                updated++;
            }
            catch { errors++; }
        }

        return new BackfillResult(updated, skipped, errors,
            $"Hormone/peptide backfill complete: {updated} updated, {skipped} skipped, {errors} errors out of {items.Count} items.");
    }
}
