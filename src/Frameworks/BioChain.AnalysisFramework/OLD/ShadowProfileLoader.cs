using YamlDotNet.Serialization;

namespace BioChain.AnalysisFramework.OLD;

/// <summary>
/// Loads and caches ShadowProfiles.yaml — the gold-standard level descriptions
/// for shadow-anchored dimension scoring.
/// Structure: dimension → mode(work/private) → level(1-5) → chemical → description text.
/// </summary>
public static class ShadowProfileLoader
{
    private static readonly Lazy<Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, string>>>>> Cache = new(Load);

    /// <summary>
    /// Get level description texts for a specific dimension/mode/chemical.
    /// Returns null if no shadow data exists for that combination.
    /// </summary>
    public static Dictionary<int, string>? GetLevelTexts(string dimension, string mode, string chemical)
    {
        var data = Cache.Value;
        if (!data.TryGetValue(dimension, out var modes)) return null;
        if (!modes.TryGetValue(mode, out var levels)) return null;

        var result = new Dictionary<int, string>();
        foreach (var (level, chemicals) in levels)
        {
            if (chemicals.TryGetValue(chemical, out var text))
                result[level] = text;
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Get all chemicals that have shadow data for a given dimension and mode.
    /// </summary>
    public static IReadOnlyList<string> GetChemicalsForDimension(string dimension, string mode)
    {
        var data = Cache.Value;
        if (!data.TryGetValue(dimension, out var modes)) return [];
        if (!modes.TryGetValue(mode, out var levels)) return [];

        var chemicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, chemDict) in levels)
        foreach (var chem in chemDict.Keys)
            chemicals.Add(chem);

        return [.. chemicals];
    }

    /// <summary>All dimension names present in shadow profiles.</summary>
    public static IReadOnlyList<string> GetDimensions() => [.. Cache.Value.Keys];

    /// <summary>
    /// Get all (dimension, mode, chemical, level, text) entries for bulk embedding.
    /// </summary>
    public static List<(string Dim, string Mode, string Chem, int Level, string Text)> GetAllEntries()
    {
        var data = Cache.Value;
        var entries = new List<(string, string, string, int, string)>();
        foreach (var (dim, modes) in data)
        foreach (var (mode, levels) in modes)
        foreach (var (level, chemicals) in levels)
        foreach (var (chem, text) in chemicals)
            entries.Add((dim, mode, chem, level, text));
        return entries;
    }

    private static Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, string>>>> Load()
    {
        var path = FindYamlPath();
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder().Build();
        var raw = deserializer.Deserialize<Dictionary<string, object>>(yaml)
                  ?? throw new InvalidOperationException("ShadowProfiles.yaml parsed as null");

        var result = new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, string>>>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (dimName, dimValue) in raw)
        {
            if (dimValue is not Dictionary<object, object> modes) continue;

            var modeDict = new Dictionary<string, Dictionary<int, Dictionary<string, string>>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var (modeKey, modeValue) in modes)
            {
                var modeName = modeKey.ToString() ?? "";
                if (modeValue is not Dictionary<object, object> levels) continue;

                var levelDict = new Dictionary<int, Dictionary<string, string>>();

                foreach (var (levelKey, levelValue) in levels)
                {
                    if (!int.TryParse(levelKey.ToString(), out var level)) continue;
                    if (levelValue is not Dictionary<object, object> chemicals) continue;

                    var chemDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (chemKey, chemValue) in chemicals)
                    {
                        var chemName = chemKey.ToString() ?? "";
                        var text = chemValue?.ToString() ?? "";
                        if (text.Length > 0)
                            chemDict[chemName] = text;
                    }

                    if (chemDict.Count > 0)
                        levelDict[level] = chemDict;
                }

                if (levelDict.Count > 0)
                    modeDict[modeName] = levelDict;
            }

            if (modeDict.Count > 0)
                result[dimName] = modeDict;
        }

        return result;
    }

    private static string FindYamlPath()
    {
        // Try relative to assembly
        var assemblyDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(assemblyDir, "Constants", "ShadowProfiles.yaml"),
            Path.Combine(assemblyDir, "ShadowProfiles.yaml"),
            // Dev-time: navigate up from bin/Debug/net9.0
            Path.Combine(assemblyDir, "..", "..", "..", "Constants", "ShadowProfiles.yaml"),
            // From Server project referencing AnalysisFramework
            Path.Combine(assemblyDir, "..", "..", "..", "..", "BioChain.AnalysisFramework", "Constants", "ShadowProfiles.yaml"),
        };

        foreach (var path in candidates)
        {
            var resolved = Path.GetFullPath(path);
            if (File.Exists(resolved))
                return resolved;
        }

        throw new FileNotFoundException(
            "ShadowProfiles.yaml not found. Searched: " + string.Join(", ", candidates.Select(Path.GetFullPath)));
    }
}
