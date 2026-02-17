using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using NeuroGateway.AnalysisFramework;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

/// <summary>
/// Embeds shadow profile level descriptions and compares person reasoning embeddings
/// against them to estimate activation levels (1.0 - 5.0).
///
/// PostgreSQL-backed cache: embeddings are loaded from the shadow_embedding table
/// on first use. Missing entries are embedded via Ollama and persisted to DB.
/// Subsequent container restarts load instantly from DB (~1200 rows).
/// </summary>
public class ShadowAnchorService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ShadowEmbeddingRepository shadowRepo)
{
    // In-process cache loaded from PostgreSQL on first use
    private readonly ConcurrentDictionary<(string Dim, string Mode, string Chem, int Level), float[]> _cache = new();

    // Per-dimension centroid: mean of shadow level embeddings (for relevance filtering)
    private readonly ConcurrentDictionary<(string Dim, string Mode), float[]> _centroids = new();

    // Ensures DB load + embedding of missing entries happens exactly once
    private readonly Lazy<Task> _initTask = new(async () => { }, isThreadSafe: true);
    private int _initialized;

    /// <summary>
    /// Compare a person's reasoning embedding against the 5 shadow level descriptions.
    /// Returns a continuous level estimate (1.0 - 5.0) via softmax-weighted interpolation.
    /// </summary>
    public async Task<float> EstimateLevelAsync(string dimension, string mode, string chemical, float[] reasoningEmbedding)
    {
        await EnsureInitializedAsync();

        var levelTexts = ShadowProfileLoader.GetLevelTexts(dimension, mode, chemical);
        if (levelTexts is null || levelTexts.Count == 0)
            return 3.0f;

        var similarities = new List<(int Level, float Sim)>();

        foreach (var (level, _) in levelTexts)
        {
            if (!_cache.TryGetValue((dimension, mode, chemical, level), out var levelEmb))
                continue;

            var sim = CosineSimilarity(reasoningEmbedding, levelEmb);
            similarities.Add((level, sim));
        }

        if (similarities.Count == 0)
            return 3.0f;

        // Softmax-weighted interpolation over levels (temperature=0.1 for sharp distribution)
        var maxSim = similarities.Max(s => s.Sim);
        float weightedSum = 0, weightSum = 0;
        foreach (var (level, sim) in similarities)
        {
            var w = MathF.Exp((sim - maxSim) / 0.1f);
            weightedSum += level * w;
            weightSum += w;
        }

        return weightSum > 0 ? Math.Clamp(weightedSum / weightSum, 1f, 5f) : 3.0f;
    }

    /// <summary>
    /// Quick relevance check using dimension centroid.
    /// If reasoning embedding is too distant from the dimension's shadow anchor space, skip it.
    /// </summary>
    public async Task<bool> IsRelevantAsync(string dimension, string mode, float[] reasoningEmbedding, float threshold = 0.3f)
    {
        await EnsureInitializedAsync();
        if (!_centroids.TryGetValue((dimension, mode), out var centroid))
            return true;
        return CosineSimilarity(reasoningEmbedding, centroid) >= threshold;
    }

    /// <summary>
    /// Get the pre-computed dimension centroid (mean-pooled shadow embeddings).
    /// </summary>
    public async Task<float[]?> GetDimensionCentroidAsync(string dimension, string mode)
    {
        await EnsureInitializedAsync();
        return _centroids.TryGetValue((dimension, mode), out var centroid) ? centroid : null;
    }

    /// <summary>
    /// Load all embeddings from PostgreSQL cache, embed any missing entries, persist them.
    /// Thread-safe: runs exactly once, subsequent calls are no-ops.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
        {
            // Already initialized or in progress — wait for completion
            while (_cache.IsEmpty && _initialized == 1)
                await Task.Delay(100);
            return;
        }

        try
        {
            // 1. Load from PostgreSQL
            var existing = await shadowRepo.LoadAllAsync();
            foreach (var (key, vec) in existing)
                _cache.TryAdd(key, vec);

            Console.WriteLine($"[ShadowAnchor] Loaded {existing.Count} embeddings from PostgreSQL");

            // 2. Find entries in YAML but missing from DB
            var allEntries = ShadowProfileLoader.GetAllEntries();
            var missing = allEntries
                .Where(e => !_cache.ContainsKey((e.Dim, e.Mode, e.Chem, e.Level)))
                .ToList();

            if (missing.Count > 0)
            {
                Console.WriteLine($"[ShadowAnchor] Embedding {missing.Count} missing shadow descriptions...");

                var toSave = new List<(string Dim, string Mode, string Chem, int Level, float[] Embedding)>();

                // Embed in batches (Ollama handles batch embedding via GenerateAsync(IList<string>))
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

                // Persist to PostgreSQL
                await shadowRepo.SaveBatchAsync(toSave);
                Console.WriteLine($"[ShadowAnchor] Persisted {toSave.Count} new embeddings to PostgreSQL");
            }

            // 3. Build dimension centroids
            BuildCentroids();

            Console.WriteLine($"[ShadowAnchor] Ready — {_cache.Count} cached, {_centroids.Count} centroids");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShadowAnchor] Init failed: {ex.Message}");
            Interlocked.Exchange(ref _initialized, 0); // allow retry
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
            if (vectors.Count == 0) continue;

            var dim = vectors[0].Length;
            var centroid = new float[dim];
            foreach (var vec in vectors)
                for (var i = 0; i < dim; i++)
                    centroid[i] += vec[i];

            for (var i = 0; i < dim; i++)
                centroid[i] /= vectors.Count;

            _centroids.TryAdd(group.Key, centroid);
        }
    }

    internal static float CosineSimilarity(float[] a, float[] b)
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
