using System.Collections.Concurrent;

namespace BioChain.Kernel.Prompts;

/// <summary>
/// Loads and caches prompt text files from disk.
/// Searches relative to <see cref="AppContext.BaseDirectory"/> with fallback paths.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class FilePromptStore : IPromptStore
{
    private readonly ConcurrentDictionary<string, string?> _cache = new();

    public string? Load(string fileName)
    {
        return _cache.GetOrAdd(fileName, name =>
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Data", name),
                Path.GetFullPath(
                    Path.Combine("..", "..", "..", "..", "Kernel", "BioChain.Kernel", "Prompts", "Data", name),
                    AppContext.BaseDirectory),
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

    public string LoadOrDefault(string fileName, string fallback)
        => Load(fileName) ?? fallback;
}
