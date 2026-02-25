namespace BioChain.AnalysisFramework.OLD;

// Pure math for shadow-anchored level estimation.
// No async, no cache — takes pre-resolved embeddings as input.
public static class LevelEstimator
{
    private const float SoftmaxTemperature = 0.1f;

    // Softmax-weighted interpolation over shadow level embeddings.
    // Returns a continuous level estimate (1.0 - 5.0).
    public static float EstimateLevel(float[] reasoningEmbedding, IReadOnlyList<(int Level, float[] Embedding)> shadowLevels)
    {
        if (shadowLevels.Count == 0)
            return 3.0f;

        var similarities = new List<(int Level, float Sim)>(shadowLevels.Count);
        foreach (var (level, emb) in shadowLevels)
        {
            var sim = EmbeddingMath.CosineSimilarity(reasoningEmbedding, emb);
            similarities.Add((level, sim));
        }

        var maxSim = similarities.Max(s => s.Sim);
        float weightedSum = 0, weightSum = 0;
        foreach (var (level, sim) in similarities)
        {
            var w = MathF.Exp((sim - maxSim) / SoftmaxTemperature);
            weightedSum += level * w;
            weightSum += w;
        }

        return weightSum > 0 ? Math.Clamp(weightedSum / weightSum, 1f, 5f) : 3.0f;
    }

    // Quick relevance check: is the reasoning embedding close enough to the dimension's shadow space?
    public static bool IsRelevant(float[] reasoningEmbedding, float[] dimensionCentroid, float threshold = 0.3f)
        => EmbeddingMath.CosineSimilarity(reasoningEmbedding, dimensionCentroid) >= threshold;

    // Map intensity_factor (-1.0 to +1.0) to level (1.0 to 5.0)
    public static float MapIntensityToLevel(float intensityFactor)
        => Math.Clamp((intensityFactor + 1f) * 2f + 1f, 1f, 5f);

    // Sigmoid confidence: 1 / (1 + exp(-(count - threshold)))
    public static float Sigmoid(int count, float threshold)
        => 1f / (1f + MathF.Exp(-(count - threshold)));
}
