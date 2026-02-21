using NeuroGateway.AnalysisFramework;
using NeuroGateway.AnalysisFramework.BigFive;
using NeuroGateway.AnalysisFramework.Mbti;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

// Infrastructure wrapper: loads observation data from DB, manages prototype
// embedding cache, delegates classification to BigFiveClassifier (pure math).
public class BigFiveService(
    EmbeddingService _embeddingService,
    ShadowEmbeddingRepository _shadowRepo,
    ProfileRepository _profileRepo)
{
    private Dictionary<string, Dictionary<string, float[]>>? _prototypeCache;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task<BigFiveResult> ClassifyAsync(string person)
    {
        var prototypes = await EnsurePrototypesAsync();
        var entries = await _profileRepo.GetProfileEntriesAsync(person);

        if (entries.Count == 0)
            return new BigFiveResult([],
                "No observation data available for Big Five classification.");

        var observations = entries
            .Select(e => (e.Chemical, e.Embedding))
            .ToList();

        // Reuse MBTI's BuildPersonChemVectors — same mean-pooling logic
        var personChemVectors = MbtiClassifier.BuildPersonChemVectors(observations);
        return BigFiveClassifier.Classify(personChemVectors, prototypes);
    }

    // Clear in-memory cache so next classify call regenerates from DB (or re-embeds)
    public async Task<int> ReembedAsync()
    {
        var deleted = await _shadowRepo.DeleteByModeAsync("bigfive_chem");
        _prototypeCache = null;
        Console.WriteLine($"[BigFive] Cleared {deleted} cached prototype embeddings — will regenerate on next classify");
        return deleted;
    }

    // Lazy-initialize: load from DB or generate + persist per-chemical prototype embeddings
    private async Task<Dictionary<string, Dictionary<string, float[]>>> EnsurePrototypesAsync()
    {
        if (_prototypeCache is not null) return _prototypeCache;

        await _initLock.WaitAsync();
        try
        {
            if (_prototypeCache is not null) return _prototypeCache;

            var filtered = await _shadowRepo.LoadByModeAsync("bigfive_chem", BigFivePrototypes.PrototypeVersion);
            var cached = new Dictionary<string, Dictionary<string, float[]>>(StringComparer.OrdinalIgnoreCase);
            int cachedCount = 0;

            foreach (var ((dim, chem), embedding) in filtered)
            {
                if (!cached.TryGetValue(dim, out var chemDict))
                {
                    chemDict = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
                    cached[dim] = chemDict;
                }
                chemDict[chem] = embedding;
                cachedCount++;
            }

            var expectedCount = BigFivePrototypes.ChemicalDescriptions.Count;

            if (cachedCount >= expectedCount)
            {
                _prototypeCache = cached;
                Console.WriteLine($"[BigFive] Loaded {cachedCount} v{BigFivePrototypes.PrototypeVersion} per-chemical prototype embeddings from DB");
                return _prototypeCache;
            }

            Console.WriteLine($"[BigFive] Generating v{BigFivePrototypes.PrototypeVersion} per-chemical prototype embeddings ({cachedCount}/{expectedCount} cached)...");
            var toSave = new List<(string Dim, string Mode, string Chem, int Level, float[] Embedding)>();

            foreach (var ((poleKey, chemical), description) in BigFivePrototypes.ChemicalDescriptions)
            {
                if (cached.TryGetValue(poleKey, out var existingDict) && existingDict.ContainsKey(chemical))
                    continue;

                var vectorStr = await _embeddingService.GenerateVectorAsync(description);
                var embedding = ParseVector(vectorStr);

                if (!cached.TryGetValue(poleKey, out var chemDict))
                {
                    chemDict = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
                    cached[poleKey] = chemDict;
                }
                chemDict[chemical] = embedding;
                toSave.Add((poleKey, "bigfive_chem", chemical, BigFivePrototypes.PrototypeVersion, embedding));
            }

            if (toSave.Count > 0)
            {
                await _shadowRepo.SaveBatchAsync(toSave);
                Console.WriteLine($"[BigFive] Saved {toSave.Count} new per-chemical prototype embeddings");
            }

            _prototypeCache = cached;
            return _prototypeCache;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static float[] ParseVector(string vectorStr)
    {
        var inner = vectorStr.Trim('[', ']');
        return inner.Split(',')
            .Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }
}
