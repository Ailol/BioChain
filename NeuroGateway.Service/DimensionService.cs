using System.Collections.Concurrent;
using NeuroGateway.AnalysisFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class DimensionService(
    EmbeddingService embeddingService,
    ProfileRepository profileRepo,
    ProfileService profileService)
{
    private const int TopK = 20;
    private const float DecayLambda = 0.01f;         // half-life ~69 days
    private const float DensityThreshold = 0.3f;
    private const float WeightCosine = 0.7f;
    private const float WeightCoherence = 0.2f;
    private const float WeightDensity = 0.1f;

    // Cache dimension description embeddings forever (static text)
    private readonly ConcurrentDictionary<string, string> _dimEmbeddingCache = new();

    public async Task<List<DimensionScore>> ScoreAsync(string person)
    {
        // Load all person data once
        var allEmbeddings = await profileRepo.GetAllEmbeddingsAsync(person);
        if (allEmbeddings.Count == 0)
            return DimensionDefinitions.All
                .Select(d => new DimensionScore(d.Name, d.Category, 0, 0f, 0, []))
                .ToList();

        var chemicalCounts = await profileService.GetChemicalCountsAsync(person);
        var countMap = chemicalCounts.ToDictionary(c => c.Chemical, c => c.Count, StringComparer.OrdinalIgnoreCase);

        // Compute per-layer centroids once
        var layerCentroids = ComputeLayerCentroids(allEmbeddings);

        var results = new List<DimensionScore>(DimensionDefinitions.All.Count);

        foreach (var dim in DimensionDefinitions.All)
        {
            var dimVec = await GetOrEmbedDimensionAsync(dim.Name, dim.Description);
            var score = await ScoreSingleAsync(person, dim.Name, dim.Category, dimVec,
                allEmbeddings, countMap, layerCentroids);
            results.Add(score);
        }

        return results;
    }

    public async Task<DimensionScore> ScoreCustomAsync(string person, string query)
    {
        var allEmbeddings = await profileRepo.GetAllEmbeddingsAsync(person);
        if (allEmbeddings.Count == 0)
            return new DimensionScore(query, "Custom", 0, 0f, 0, []);

        var chemicalCounts = await profileService.GetChemicalCountsAsync(person);
        var countMap = chemicalCounts.ToDictionary(c => c.Chemical, c => c.Count, StringComparer.OrdinalIgnoreCase);
        var layerCentroids = ComputeLayerCentroids(allEmbeddings);

        // Custom queries are NOT cached (dynamic input)
        var dimVec = await embeddingService.GenerateVectorAsync(query);
        return await ScoreSingleAsync(person, query, "Custom", dimVec,
            allEmbeddings, countMap, layerCentroids);
    }

    private async Task<DimensionScore> ScoreSingleAsync(
        string person, string name, string category, string dimVec,
        List<(string Chemical, float[] Embedding, DateTime CreatedAt)> allEmbeddings,
        Dictionary<string, int> countMap,
        Dictionary<string, float[]> layerCentroids)
    {
        // Signal 1: Weighted cosine similarity with temporal decay + frequency boost
        var topMatches = await profileRepo.GetSimilarReasoningsAsync(person, dimVec, TopK);

        var now = DateTime.UtcNow;
        float weightedSum = 0, weightSum = 0;
        var evidence = new List<DimensionEvidence>();

        foreach (var (chemical, reasoning, similarity, createdAt) in topMatches)
        {
            var daysSince = (float)(now - createdAt).TotalDays;
            var recency = MathF.Exp(-DecayLambda * daysSince);
            var freq = countMap.GetValueOrDefault(chemical, 1);
            var freqBoost = 1f + MathF.Log(1f + freq) * 0.1f;
            var weight = recency * freqBoost;

            weightedSum += similarity * weight;
            weightSum += weight;

            var layer = DimensionDefinitions.ChemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";
            evidence.Add(new DimensionEvidence(chemical, layer, reasoning, similarity, recency));
        }

        var weightedCosineMean = weightSum > 0 ? weightedSum / weightSum : 0f;

        // Signal 2: Cross-layer coherence
        var coherence = ComputeCoherence(dimVec, layerCentroids);

        // Signal 3: Frequency density
        var relevantCount = topMatches.Count(m => m.Similarity > DensityThreshold);
        var density = allEmbeddings.Count > 0 ? (float)relevantCount / allEmbeddings.Count : 0f;

        // Composite
        var rawScore = weightedCosineMean * WeightCosine + coherence * WeightCoherence + density * WeightDensity;
        var score = (int)Math.Clamp(rawScore * 100, 0, 100);

        return new DimensionScore(name, category, score, coherence, evidence.Count, evidence);
    }

    private async Task<string> GetOrEmbedDimensionAsync(string name, string description)
    {
        if (_dimEmbeddingCache.TryGetValue(name, out var cached))
            return cached;

        var vec = await embeddingService.GenerateVectorAsync(description);
        _dimEmbeddingCache.TryAdd(name, vec);
        return vec;
    }

    private static Dictionary<string, float[]> ComputeLayerCentroids(
        List<(string Chemical, float[] Embedding, DateTime CreatedAt)> allEmbeddings)
    {
        var groups = new Dictionary<string, List<float[]>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (chemical, embedding, _) in allEmbeddings)
        {
            var layer = DimensionDefinitions.ChemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";
            if (!groups.TryGetValue(layer, out var list))
            {
                list = [];
                groups[layer] = list;
            }
            list.Add(embedding);
        }

        var centroids = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (layer, embeddings) in groups)
        {
            if (embeddings.Count == 0) continue;
            var dim = embeddings[0].Length;
            var centroid = new float[dim];
            foreach (var emb in embeddings)
                for (var i = 0; i < dim; i++)
                    centroid[i] += emb[i];
            for (var i = 0; i < dim; i++)
                centroid[i] /= embeddings.Count;
            centroids[layer] = centroid;
        }

        return centroids;
    }

    private static float ComputeCoherence(string dimVecLiteral, Dictionary<string, float[]> layerCentroids)
    {
        if (layerCentroids.Count < 2) return 0.5f;

        var dimVec = ParseVectorLiteral(dimVecLiteral);
        var layerSims = new List<float>();

        foreach (var (_, centroid) in layerCentroids)
        {
            var sim = CosineSimilarity(dimVec, centroid);
            layerSims.Add(sim);
        }

        if (layerSims.Count < 2) return 0.5f;

        var mean = layerSims.Average();
        if (mean <= 0.001f) return 0f;

        var variance = layerSims.Sum(s => (s - mean) * (s - mean)) / layerSims.Count;
        var stddev = MathF.Sqrt(variance);
        var cv = stddev / MathF.Abs(mean);

        return Math.Clamp(1f - cv, 0f, 1f);
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

    private static float[] ParseVectorLiteral(string literal)
    {
        var trimmed = literal.Trim('[', ']');
        var parts = trimmed.Split(',');
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
        return result;
    }
}
