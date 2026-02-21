namespace NeuroGateway.AnalysisFramework;

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
}
