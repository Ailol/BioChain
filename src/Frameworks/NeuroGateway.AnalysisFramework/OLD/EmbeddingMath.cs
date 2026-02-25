using System.Globalization;

namespace NeuroGateway.AnalysisFramework.OLD;

// Pure vector math for embedding operations
public static class EmbeddingMath
{
    public static float CosineSimilarity(float[] a, float[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom > 0 ? dot / denom : 0f;
    }

    public static float[]? MeanPool(List<float[]> embeddings)
    {
        if (embeddings.Count == 0) return null;
        var dim = embeddings[0].Length;
        var result = new float[dim];
        foreach (var vec in embeddings)
            for (var i = 0; i < dim; i++)
                result[i] += vec[i];
        for (var i = 0; i < dim; i++)
            result[i] /= embeddings.Count;
        return result;
    }

    // Parse a pgvector text representation "[0.1,0.2,...]" into float[].
    public static float[] ParseVector(string vectorStr)
    {
        var trimmed = vectorStr.Trim('[', ']');
        var parts = trimmed.Split(',');
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }

    // Mean-pool observation embeddings grouped by chemical.
    public static Dictionary<string, float[]> BuildChemicalVectors(
        IReadOnlyList<(string Chemical, float[] Embedding)> observations)
    {
        var result = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        var grouped = observations.GroupBy(e => e.Chemical, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var embeddings = group.Select(e => e.Embedding).ToList();
            var pooled = MeanPool(embeddings);
            if (pooled is not null)
                result[group.Key] = pooled;
        }

        return result;
    }
}
