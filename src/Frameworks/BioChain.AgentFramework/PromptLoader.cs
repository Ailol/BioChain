using System.Collections.Concurrent;

namespace BioChain.AgentFramework;

/// <summary>
/// Loads and caches prompt text files from disk.
/// Searches relative to <see cref="AppContext.BaseDirectory"/> with fallback paths.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public static class PromptLoader
{
    private static readonly ConcurrentDictionary<string, string?> Cache = new();

    /// <summary>
    /// Loads a prompt file by name. Searches <c>Data/{fileName}</c> first,
    /// then <c>../../../../Libraries/BioChain.Repository/Data/{fileName}</c>.
    /// Returns null if not found. Caches result.
    /// </summary>
    public static string? Load(string fileName)
    {
        return Cache.GetOrAdd(fileName, static name =>
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Data", name),
                Path.GetFullPath(
                    Path.Combine("..", "..", "..", "..", "Libraries", "BioChain.Repository", "Data", name),
                    AppContext.BaseDirectory),
                Path.GetFullPath($"../BioChain.Repository/Data/{name}", AppContext.BaseDirectory),
            };

            foreach (var path in candidates)
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full))
                    return File.ReadAllText(full).Trim();
            }
            return null;
        });
    }

    /// <summary>
    /// Loads with fallback text when file is not found.
    /// </summary>
    public static string LoadOrDefault(string fileName, string fallback)
        => Load(fileName) ?? fallback;
}
