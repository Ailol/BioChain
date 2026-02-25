using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using BioChain.AnalysisFramework;
using BioChain.Repository;

namespace BioChain.Service;

// Manages embedding-based level estimation using cached reference embeddings.
// PostgreSQL-backed via embedding_cache table (replaces shadow profiles YAML).
public class ShadowAnchorService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    EmbeddingCacheRepository cacheRepo)
{
    private readonly ConcurrentDictionary<string, float[]> _cache = new();
    private readonly ConcurrentDictionary<string, float[]> _centroids = new();
    private int _initialized;

    // Estimate level for a signal within a dimension context using embedding similarity
    // to cached reference embeddings. Returns 1-5 scale.
    public async Task<float> EstimateLevelAsync(string dimension, string mode, string signal, float[] embedding)
    {
        await EnsureInitializedAsync();

        // Look for cached level embeddings for this dim/mode/signal combination
        var shadowLevels = new List<(int Level, float[] Embedding)>();
        for (var level = 1; level <= 5; level++)
        {
            var key = $"{dimension}|{mode}|{signal}|{level}";
            if (_cache.TryGetValue(key, out var levelEmb))
                shadowLevels.Add((level, levelEmb));
        }

        if (shadowLevels.Count == 0)
            return 3.0f; // neutral default

        return LevelEstimator.EstimateLevel(embedding, shadowLevels);
    }

    // Quick relevance check using dimension centroid
    public async Task<bool> IsRelevantAsync(string dimension, string mode, float[] embedding, float threshold = 0.3f)
    {
        await EnsureInitializedAsync();
        var centroidKey = $"{dimension}|{mode}";
        if (!_centroids.TryGetValue(centroidKey, out var centroid))
            return true;
        return LevelEstimator.IsRelevant(embedding, centroid, threshold);
    }

    public async Task<float[]?> GetDimensionCentroidAsync(string dimension, string mode)
    {
        await EnsureInitializedAsync();
        var centroidKey = $"{dimension}|{mode}";
        return _centroids.TryGetValue(centroidKey, out var centroid) ? centroid : null;
    }

    private async Task EnsureInitializedAsync()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
        {
            while (_cache.IsEmpty && _initialized == 1)
                await Task.Delay(100);
            return;
        }

        try
        {
            // Load all shadow-level embeddings from embedding_cache
            var existing = await cacheRepo.LoadByTypeAsync("shadow_level");
            foreach (var (key, vec) in existing)
                _cache.TryAdd(key, vec);

            Console.WriteLine($"[ShadowAnchor] Loaded {existing.Count} embeddings from embedding_cache");

            BuildCentroids();
            Console.WriteLine($"[ShadowAnchor] Ready — {_cache.Count} cached, {_centroids.Count} centroids");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShadowAnchor] Init failed: {ex.Message}");
            Interlocked.Exchange(ref _initialized, 0);
            throw;
        }
    }

    private void BuildCentroids()
    {
        // Group by dimension|mode prefix
        var groups = _cache
            .GroupBy(kv =>
            {
                var parts = kv.Key.Split('|');
                return parts.Length >= 2 ? $"{parts[0]}|{parts[1]}" : kv.Key;
            })
            .ToList();

        foreach (var group in groups)
        {
            var vectors = group.Select(kv => kv.Value).ToList();
            var centroid = EmbeddingMath.MeanPool(vectors);
            if (centroid is not null)
                _centroids.TryAdd(group.Key, centroid);
        }
    }
}
