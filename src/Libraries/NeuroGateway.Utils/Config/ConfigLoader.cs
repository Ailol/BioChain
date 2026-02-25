using System.Text.Json;

namespace NeuroGateway.Utils;

/// <summary>
/// Shared utility for loading JSON config files and prompt templates.
/// Replaces duplicated LoadConfig/LoadPrompts methods across services.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Load and deserialize a JSON file from the Config/ directory.
    /// Searches AppContext.BaseDirectory first, then CurrentDirectory.
    /// Returns new T() if file not found.
    /// </summary>
    public static T LoadJson<T>(string filename) where T : new()
    {
        var path = ResolveConfigPath(filename);
        if (path == null)
            return new T();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
    }

    /// <summary>
    /// Load a text file from the Prompts/ directory.
    /// Searches AppContext.BaseDirectory first, then CurrentDirectory.
    /// </summary>
    public static string LoadPromptText(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", filename);
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Prompts", filename);

        return File.ReadAllText(path);
    }

    private static string? ResolveConfigPath(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Config", filename);
        if (File.Exists(path))
            return path;

        path = Path.Combine(Directory.GetCurrentDirectory(), "Config", filename);
        return File.Exists(path) ? path : null;
    }
}
