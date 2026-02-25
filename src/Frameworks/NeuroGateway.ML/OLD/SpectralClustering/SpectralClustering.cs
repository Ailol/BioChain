using NeuroGateway.ML.OLD;

namespace NeuroGateway.ML.OLD.SpectralClustering;

/// <summary>
/// Spectral clustering on embedding similarity graphs → behavioral archetypes.
/// Builds cosine-similarity affinity matrix, normalized graph Laplacian,
/// extracts eigenvectors, clusters with k-means in spectral space.
/// Input: embedding vectors (1536-dim). Output: behavioral archetype assignments.
/// </summary>
public static class SpectralClusterer
{
    public record Archetype(int Id, string Label, float[] Centroid, int MemberCount);
    public record Assignment(int Index, int ClusterId, float Confidence);

    public record SpectralResult(
        List<Archetype> Archetypes,
        List<Assignment> Assignments,
        float Modularity,
        float[][] SpectralCoords);

    public static SpectralResult Cluster(
        IReadOnlyList<float[]> embeddings,
        int k = 4,
        float sigma = 0f,
        int maxIter = 100)
    {
        var n = embeddings.Count;
        if (n < k) k = Math.Max(1, n);
        if (n <= 1) return SingletonResult(embeddings);

        // 1. Affinity matrix (cosine similarity, optionally Gaussian kernel)
        var W = BuildAffinity(embeddings, n, sigma);

        // 2. Degree vector + D^{-1/2}
        var dInvSqrt = new float[n];
        for (var i = 0; i < n; i++)
        {
            float deg = 0;
            for (var j = 0; j < n; j++) deg += W[i][j];
            dInvSqrt[i] = deg > 1e-10f ? 1f / MathF.Sqrt(deg) : 0f;
        }

        // 3. Normalized affinity M = D^{-1/2} W D^{-1/2}, find top-k eigenvectors
        var normalizedW = LinearAlgebra.NewMatrix(n, n);
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                normalizedW[i][j] = dInvSqrt[i] * W[i][j] * dInvSqrt[j];

        var (_, eigVecs) = LinearAlgebra.TopKEigen(normalizedW, n, k);

        // 4. Row-normalize spectral coordinates
        for (var i = 0; i < n; i++)
        {
            float norm = 0;
            for (var j = 0; j < k; j++) norm += eigVecs[i][j] * eigVecs[i][j];
            norm = MathF.Sqrt(norm);
            if (norm > 1e-10f)
                for (var j = 0; j < k; j++) eigVecs[i][j] /= norm;
        }

        // 5. K-means in spectral space
        var (assignments, centroids) = LinearAlgebra.KMeans(eigVecs, n, k, k, maxIter);

        // 6. Confidence per point
        var confidences = new float[n];
        for (var i = 0; i < n; i++)
        {
            var own = LinearAlgebra.SqDist(eigVecs[i], centroids[assignments[i]], k);
            var nextBest = float.MaxValue;
            for (var c = 0; c < k; c++)
                if (c != assignments[i])
                    nextBest = MathF.Min(nextBest, LinearAlgebra.SqDist(eigVecs[i], centroids[c], k));
            confidences[i] = nextBest < 1e-10f ? 0.5f : MathF.Min(1f, nextBest / (own + nextBest));
        }

        // 7. Build archetypes from original embeddings
        var dim = embeddings[0].Length;
        var archetypes = new List<Archetype>(k);
        for (var c = 0; c < k; c++)
        {
            var members = new List<float[]>();
            for (var i = 0; i < n; i++)
                if (assignments[i] == c) members.Add(embeddings[i]);

            var centroid = members.Count > 0 ? LinearAlgebra.MeanPool(members) : new float[dim];
            archetypes.Add(new Archetype(c, $"archetype_{c}", centroid, members.Count));
        }

        // 8. Modularity
        float totalW = 0;
        var degrees = new float[n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++) { degrees[i] += W[i][j]; totalW += W[i][j]; }
        float q = 0;
        if (totalW > 1e-10f)
        {
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                    if (assignments[i] == assignments[j])
                        q += W[i][j] - degrees[i] * degrees[j] / totalW;
            q /= totalW;
        }

        var result = new List<Assignment>(n);
        for (var i = 0; i < n; i++)
            result.Add(new Assignment(i, assignments[i], confidences[i]));

        return new SpectralResult(archetypes, result, q, eigVecs);
    }

    private static float[][] BuildAffinity(IReadOnlyList<float[]> embeddings, int n, float sigma)
    {
        var W = LinearAlgebra.NewMatrix(n, n);
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var sim = LinearAlgebra.CosineSimilarity(embeddings[i], embeddings[j]);
                float w = sigma > 0f
                    ? MathF.Exp(-((1f - sim) * (1f - sim)) / (2f * sigma * sigma))
                    : MathF.Max(0f, sim);
                W[i][j] = w;
                W[j][i] = w;
            }
        }
        return W;
    }

    private static SpectralResult SingletonResult(IReadOnlyList<float[]> embeddings)
    {
        var archetypes = new List<Archetype> { new(0, "archetype_0", embeddings.Count > 0 ? embeddings[0] : [], embeddings.Count) };
        var assignments = new List<Assignment>();
        for (var i = 0; i < embeddings.Count; i++)
            assignments.Add(new Assignment(i, 0, 1f));
        return new SpectralResult(archetypes, assignments, 0f, []);
    }
}
