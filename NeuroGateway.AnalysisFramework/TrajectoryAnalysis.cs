using NeuroGateway.Models;

namespace NeuroGateway.AnalysisFramework;

// Linear regression over timestamped activation levels + semantic drift detection.
public static class TrajectoryAnalysis
{
    private const float SemanticDriftThreshold = 0.7f;

    // Compute temporal trajectory: slope (level-change/day), direction, R²,
    // boundary levels, and semantic drift detection.
    public static TemporalTrajectory? ComputeTrajectory(List<(DateTime Time, float Level, float[] Embedding)> points)
    {
        if (points.Count < 3) return null;

        var sorted = points.OrderBy(p => p.Time).ToList();
        var t0 = sorted[0].Time;

        var xs = sorted.Select(p => (float)(p.Time - t0).TotalDays).ToArray();
        var ys = sorted.Select(p => p.Level).ToArray();
        var n = xs.Length;

        // Linear regression: y = slope * x + intercept
        var xMean = xs.Average();
        var yMean = ys.Average();

        float ssXY = 0, ssXX = 0, ssTot = 0, ssRes = 0;
        for (var i = 0; i < n; i++)
        {
            ssXY += (xs[i] - xMean) * (ys[i] - yMean);
            ssXX += (xs[i] - xMean) * (xs[i] - xMean);
        }

        var slope = ssXX > 0 ? ssXY / ssXX : 0f;
        var intercept = yMean - slope * xMean;

        for (var i = 0; i < n; i++)
        {
            var predicted = slope * xs[i] + intercept;
            ssRes += (ys[i] - predicted) * (ys[i] - predicted);
            ssTot += (ys[i] - yMean) * (ys[i] - yMean);
        }
        var r2 = ssTot > 0 ? Math.Clamp(1f - ssRes / ssTot, 0f, 1f) : 0f;

        var direction = MathF.Abs(slope) < 0.005f ? "Stable"
            : slope > 0.02f ? "Rising Sharply"
            : slope > 0 ? "Rising"
            : slope < -0.02f ? "Declining Sharply"
            : "Declining";

        var (driftDetected, driftMagnitude) = DetectSemanticDrift(sorted);

        return new TemporalTrajectory(
            MathF.Round(slope, 5),
            direction,
            MathF.Round(r2, 3),
            n,
            ys[0],
            ys[^1],
            driftDetected,
            MathF.Round(driftMagnitude, 3));
    }

    // Split entries into early vs late halves and compare their mean-pooled embeddings.
    // If cosine similarity drops below threshold, reasoning content has semantically shifted.
    private static (bool Detected, float Magnitude) DetectSemanticDrift(
        List<(DateTime Time, float Level, float[] Embedding)> sorted)
    {
        if (sorted.Count < 4) return (false, 0f);

        var mid = sorted.Count / 2;
        var earlyEmbeddings = sorted.Take(mid).Select(p => p.Embedding).ToList();
        var lateEmbeddings = sorted.Skip(mid).Select(p => p.Embedding).ToList();

        var earlyCentroid = EmbeddingMath.MeanPool(earlyEmbeddings);
        var lateCentroid = EmbeddingMath.MeanPool(lateEmbeddings);

        if (earlyCentroid is null || lateCentroid is null)
            return (false, 0f);

        var similarity = EmbeddingMath.CosineSimilarity(earlyCentroid, lateCentroid);
        var driftMagnitude = 1f - similarity;
        var driftDetected = similarity < SemanticDriftThreshold;

        return (driftDetected, driftMagnitude);
    }
}
