namespace NeuroGateway.AgentFramework.Algorithms;

/// <summary>
/// Reusable vector math algorithms for embedding analysis.
/// Extracted from EmbeddingService and VectorService static methods.
/// </summary>
public static class VectorAlgorithms
{
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
    /// Aggregate multiple embeddings using mean pooling to create a centroid vector.
    /// </summary>
    public static float[] MeanPool(IEnumerable<float[]> embeddings)
    {
        var list = embeddings.Where(e => e != null).ToList();
        if (list.Count == 0) return [];

        var dim = list[0].Length;
        var result = new float[dim];

        foreach (var embedding in list)
            for (int i = 0; i < dim; i++)
                result[i] += embedding[i];

        for (int i = 0; i < dim; i++)
            result[i] /= list.Count;

        return result;
    }

    /// <summary>
    /// Format a float array as a PostgreSQL vector literal for insertion.
    /// </summary>
    public static string ToPostgresVector(float[] embedding)
    {
        if (embedding == null || embedding.Length == 0)
            return "NULL";

        return $"[{string.Join(",", embedding.Select(f => f.ToString("G9")))}]";
    }

    /// <summary>
    /// Parse a PostgreSQL vector literal back into a float array.
    /// Inverse of ToPostgresVector. Handles format "[0.1,0.2,...]".
    /// </summary>
    public static float[]? ParsePostgresVector(string? vectorText)
    {
        if (string.IsNullOrWhiteSpace(vectorText) || vectorText == "NULL")
            return null;

        var trimmed = vectorText.Trim().TrimStart('[').TrimEnd(']');
        if (string.IsNullOrEmpty(trimmed))
            return null;

        return trimmed.Split(',')
            .Select(s => float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f)
            .ToArray();
    }

}
