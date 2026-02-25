namespace BioChain.AnalysisFramework;

public static class LevelEstimator
{
    public static float EstimateLevel(float[] reasoningEmbedding, List<(int Level, float[] Embedding)> shadowLevels)
    {
        if (shadowLevels.Count == 0) return 3.0f;
        var similarities = shadowLevels
            .Select(sl => (sl.Level, Sim: EmbeddingMath.CosineSimilarity(reasoningEmbedding, sl.Embedding)))
            .ToList();
        var maxSim = similarities.Max(s => s.Sim);
        var expSims = similarities.Select(s => (s.Level, Exp: MathF.Exp((s.Sim - maxSim) * 10f))).ToList();
        var sumExp = expSims.Sum(s => s.Exp);
        if (sumExp <= 0) return 3.0f;
        return expSims.Sum(s => s.Level * (s.Exp / sumExp));
    }

    public static bool IsRelevant(float[] reasoningEmbedding, float[] centroid, float threshold = 0.3f)
        => EmbeddingMath.CosineSimilarity(reasoningEmbedding, centroid) >= threshold;

    public static float Sigmoid(int count, float threshold = 3f)
        => 1f / (1f + MathF.Exp(-(count - threshold)));
}
