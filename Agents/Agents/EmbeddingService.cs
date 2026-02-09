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

    public async Task<BackfillResult> BackfillEmbeddingsAsync(string? person = null)
    {
        var traits = await _embeddingRepo.GetTraitsWithoutEmbeddingsAsync(person);
        return await BackfillAsync(traits,
            t => GenerateTraitEmbeddingAsync(t.Topic, t.Explanation),
            (t, vec) => _embeddingRepo.UpdateTraitEmbeddingAsync(t.Id, vec),
            "Backfill", traits.Count);
    }

    public async Task<BackfillResult> BackfillHormonePeptideEmbeddingsAsync()
    {
        var items = await _embeddingRepo.GetItemsWithoutEmbeddingsAsync();
        return await BackfillAsync(items,
            i => GenerateNameEmbeddingAsync(i.Name),
            (i, vec) => _embeddingRepo.UpdateItemEmbeddingAsync(i.Table, i.Id, vec),
            "Hormone/peptide backfill", items.Count);
    }

    private async Task<BackfillResult> BackfillAsync<T>(
        IList<T> items, Func<T, Task<float[]?>> embed, Func<T, string, Task> update, string label, int total)
    {
        int updated = 0, skipped = 0, errors = 0;
        foreach (var item in items)
        {
            try
            {
                var embedding = await embed(item);
                if (embedding == null) { skipped++; continue; }
                await update(item, ToPostgresVector(embedding));
                updated++;
            }
            catch { errors++; }
        }
        return new BackfillResult(updated, skipped, errors,
            $"{label} complete: {updated} updated, {skipped} skipped, {errors} errors out of {total} items.");
    }
}
