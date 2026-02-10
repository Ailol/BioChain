using NeuroGateway.AgentFramework.Algorithms;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Generic dimension-based embedding cache. Loads text descriptions from disk,
/// embeds them via LlmService, and caches for fast centroid → nearest-label lookup.
/// Used by ProfileScoringService: "relationship" dimension for neurorespond,
/// "career" dimension for ProfessionalService (future), etc.
/// </summary>
public class ContextEmbeddingCache
{
    // dimension → { label → float[] }
    private readonly Dictionary<string, Dictionary<string, float[]>> _dimensions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Load all *.txt files from a directory as a named dimension.
    /// Each file is embedded and cached: fileName (without extension) → embedding.
    /// </summary>
    public async Task LoadDimensionAsync(string dimensionName, string directoryPath, LlmService llm)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.Error.WriteLine($"ContextEmbeddingCache: Directory not found for dimension '{dimensionName}': {directoryPath}");
            return;
        }

        var files = Directory.GetFiles(directoryPath, "*.txt");
        var dim = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var label = Path.GetFileNameWithoutExtension(file);
            var text = await File.ReadAllTextAsync(file);

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.Error.WriteLine($"ContextEmbeddingCache: Skipping empty file: {file}");
                continue;
            }

            var embedding = await llm.EmbedAsync(text);
            if (embedding != null)
            {
                dim[label] = embedding;
            }
            else
            {
                Console.Error.WriteLine($"ContextEmbeddingCache: Failed to embed {file}");
            }
        }

        _dimensions[dimensionName] = dim;
        Console.Error.WriteLine($"ContextEmbeddingCache: Loaded {dim.Count} embeddings for dimension '{dimensionName}'.");
    }

    /// <summary>
    /// Find the closest label in a dimension to a given centroid vector.
    /// Returns (label, similarity) or null if dimension not loaded or empty.
    /// </summary>
    public (string Name, float Similarity)? FindClosest(string dimensionName, float[] centroid)
    {
        if (!_dimensions.TryGetValue(dimensionName, out var dim) || dim.Count == 0)
            return null;

        string? bestLabel = null;
        float bestSimilarity = float.MinValue;

        foreach (var (label, embedding) in dim)
        {
            var sim = (float)VectorAlgorithms.CosineSimilarity(centroid, embedding);
            if (sim > bestSimilarity)
            {
                bestSimilarity = sim;
                bestLabel = label;
            }
        }

        return bestLabel != null ? (bestLabel, bestSimilarity) : null;
    }

    /// <summary>
    /// Get the raw embedding for a specific label in a dimension.
    /// Returns null if not found.
    /// </summary>
    public float[]? GetEmbedding(string dimensionName, string label)
    {
        return _dimensions.TryGetValue(dimensionName, out var dim)
            && dim.TryGetValue(label, out var vec) ? vec : null;
    }

    /// <summary>
    /// Check if a dimension is loaded and has entries.
    /// </summary>
    public bool HasDimension(string dimensionName) =>
        _dimensions.TryGetValue(dimensionName, out var dim) && dim.Count > 0;
}
