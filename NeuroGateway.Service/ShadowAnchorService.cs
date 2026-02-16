using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.AI;
using NeuroGateway.AnalysisFramework;

namespace NeuroGateway.Service;

/// <summary>
/// Embeds shadow profile level descriptions and compares person reasoning embeddings
/// against them to estimate activation levels (1.0 - 5.0).
/// </summary>
public class ShadowAnchorService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    // Cache: (dimension, mode, chemical, level) → embedding vector
    private readonly ConcurrentDictionary<(string Dim, string Mode, string Chem, int Level), float[]> _cache = new();

    /// <summary>
    /// Compare a person's reasoning embedding against the 5 shadow level descriptions.
    /// Returns a continuous level estimate (1.0 - 5.0) via softmax-weighted interpolation.
    /// </summary>
    public async Task<float> EstimateLevelAsync(string dimension, string mode, string chemical, float[] reasoningEmbedding)
    {
        var levelTexts = ShadowProfileLoader.GetLevelTexts(dimension, mode, chemical);
        if (levelTexts is null || levelTexts.Count == 0)
            return 3.0f; // fallback: mid-level if no shadow data

        var similarities = new List<(int Level, float Sim)>();

        foreach (var (level, _) in levelTexts)
        {
            var levelEmb = await GetOrEmbedAsync(dimension, mode, chemical, level);
            var sim = CosineSimilarity(reasoningEmbedding, levelEmb);
            similarities.Add((level, sim));
        }

        // Softmax-weighted interpolation over levels
        var maxSim = similarities.Max(s => s.Sim);
        float weightedSum = 0, weightSum = 0;
        foreach (var (level, sim) in similarities)
        {
            // Temperature-scaled softmax (temperature=0.1 for sharper distribution)
            var w = MathF.Exp((sim - maxSim) / 0.1f);
            weightedSum += level * w;
            weightSum += w;
        }

        return weightSum > 0 ? Math.Clamp(weightedSum / weightSum, 1f, 5f) : 3.0f;
    }

    private async Task<float[]> GetOrEmbedAsync(string dimension, string mode, string chemical, int level)
    {
        var key = (dimension, mode, chemical, level);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var levelTexts = ShadowProfileLoader.GetLevelTexts(dimension, mode, chemical);
        if (levelTexts is null || !levelTexts.TryGetValue(level, out var text))
            throw new InvalidOperationException($"No shadow text for {dimension}/{mode}/{chemical}/level{level}");

        var embedding = await embeddingGenerator.GenerateAsync(text);
        var vector = embedding.Vector.ToArray();
        _cache.TryAdd(key, vector);
        return vector;
    }

    private static float CosineSimilarity(float[] a, float[] b)
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
}
