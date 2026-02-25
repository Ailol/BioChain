using System.Globalization;

namespace NeuroGateway.AnalysisFramework;

public static class EmbeddingMath
{
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        float dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        var denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom > 0 ? dot / denom : 0f;
    }

    public static float[] ParseVector(string vectorStr)
    {
        if (string.IsNullOrWhiteSpace(vectorStr)) return [];
        var trimmed = vectorStr.Trim().TrimStart('[').TrimEnd(']');
        if (string.IsNullOrEmpty(trimmed)) return [];
        return trimmed.Split(',')
            .Select(s => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f)
            .ToArray();
    }

    public static float[]? MeanPool(List<float[]> vectors)
    {
        if (vectors.Count == 0) return null;
        var dim = vectors[0].Length;
        if (dim == 0) return null;
        var result = new float[dim];
        foreach (var vec in vectors)
            for (var i = 0; i < dim && i < vec.Length; i++)
                result[i] += vec[i];
        for (var i = 0; i < dim; i++)
            result[i] /= vectors.Count;
        return result;
    }

    public static Dictionary<string, float[]> BuildSignalVectors(
        List<(string Signal, float[] Embedding)> entries)
    {
        var groups = entries.GroupBy(e => e.Signal, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var vectors = group.Select(e => e.Embedding).ToList();
            var pooled = MeanPool(vectors);
            if (pooled is not null) result[group.Key] = pooled;
        }
        return result;
    }
}
