using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using NeuroGateway.AnalysisFramework;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

// Manages the embedding cache for shadow profile level descriptions.
// PostgreSQL-backed: loads on first use, embeds missing entries via Ollama, persists to DB.
// Delegates pure math to LevelEstimator and EmbeddingMath in AnalysisFramework.
public class ShadowAnchorService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ShadowEmbeddingRepository shadowRepo)
{
    // In-process cache loaded from PostgreSQL on first use
    private readonly ConcurrentDictionary<(string Dim, string Mode, string Chem, int Level), float[]> _cache = new();

    // Per-dimension centroid: mean of shadow level embeddings (for relevance filtering)
    private readonly ConcurrentDictionary<(string Dim, string Mode), float[]> _centroids = new();

    private int _initialized;

    // Resolve cached embeddings for a dimension/mode/chemical, then delegate to LevelEstimator.
    public async Task<float> EstimateLevelAsync(string dimension, string mode, string chemical, float[] reasoningEmbedding)
    {
        await EnsureInitializedAsync();

        var levelTexts = ShadowProfileLoader.GetLevelTexts(dimension, mode, chemical);
        if (levelTexts is null || levelTexts.Count == 0)
            return 3.0f;

        var shadowLevels = new List<(int Level, float[] Embedding)>();
        foreach (var (level, _) in levelTexts)
        {
            if (_cache.TryGetValue((dimension, mode, chemical, level), out var levelEmb))
                shadowLevels.Add((level, levelEmb));
        }

        if (shadowLevels.Count == 0)
            return 3.0f;

        return LevelEstimator.EstimateLevel(reasoningEmbedding, shadowLevels);
    }

    // Quick relevance check using dimension centroid.
    public async Task<bool> IsRelevantAsync(string dimension, string mode, float[] reasoningEmbedding, float threshold = 0.3f)
    {
        await EnsureInitializedAsync();
        if (!_centroids.TryGetValue((dimension, mode), out var centroid))
            return true;
        return LevelEstimator.IsRelevant(reasoningEmbedding, centroid, threshold);
    }

    // Get the pre-computed dimension centroid (mean-pooled shadow embeddings).
    public async Task<float[]?> GetDimensionCentroidAsync(string dimension, string mode)
    {
        await EnsureInitializedAsync();
        return _centroids.TryGetValue((dimension, mode), out var centroid) ? centroid : null;
    }

    // Load all embeddings from PostgreSQL cache, embed any missing entries, persist them.
    // Thread-safe: runs exactly once, subsequent calls are no-ops.
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
            var existing = await shadowRepo.LoadAllAsync();
            foreach (var (key, vec) in existing)
                _cache.TryAdd(key, vec);

            Console.WriteLine($"[ShadowAnchor] Loaded {existing.Count} embeddings from PostgreSQL");

            var allEntries = ShadowProfileLoader.GetAllEntries();
            var missing = allEntries
                .Where(e => !_cache.ContainsKey((e.Dim, e.Mode, e.Chem, e.Level)))
                .ToList();

            if (missing.Count > 0)
            {
                Console.WriteLine($"[ShadowAnchor] Embedding {missing.Count} missing shadow descriptions...");

                var toSave = new List<(string Dim, string Mode, string Chem, int Level, float[] Embedding)>();

                const int batchSize = 10;
                for (var i = 0; i < missing.Count; i += batchSize)
                {
                    var batch = missing.Skip(i).Take(batchSize).ToList();
                    var texts = batch.Select(e => e.Text).ToList();

                    var embeddings = await embeddingGenerator.GenerateAsync(texts);

                    for (var j = 0; j < batch.Count; j++)
                    {
                        var entry = batch[j];
                        var vector = embeddings[j].Vector.ToArray();
                        var key = (entry.Dim, entry.Mode, entry.Chem, entry.Level);
                        _cache.TryAdd(key, vector);
                        toSave.Add((entry.Dim, entry.Mode, entry.Chem, entry.Level, vector));
                    }

                    if ((i + batchSize) % 100 == 0 || i + batchSize >= missing.Count)
                        Console.WriteLine($"[ShadowAnchor] Embedded {Math.Min(i + batchSize, missing.Count)}/{missing.Count}");
                }

                await shadowRepo.SaveBatchAsync(toSave);
                Console.WriteLine($"[ShadowAnchor] Persisted {toSave.Count} new embeddings to PostgreSQL");
            }

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
        var groups = _cache
            .GroupBy(kv => (kv.Key.Dim, kv.Key.Mode))
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
