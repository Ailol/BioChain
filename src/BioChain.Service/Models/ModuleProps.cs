using System.Text.Json.Serialization;

namespace BioChain.Service.Models;

/// <summary>
/// Strongly-typed view of module.properties JSONB.
/// Tracks lifecycle: status, utility, generation, evaluation stats, watch lists.
/// </summary>
public sealed class ModuleProps
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("utility")]
    public double Utility { get; set; } = 0.5;

    [JsonPropertyName("generation")]
    public int Generation { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }

    [JsonPropertyName("hit_count")]
    public int HitCount { get; set; }

    [JsonPropertyName("last_eval")]
    public DateTimeOffset? LastEval { get; set; }

    [JsonPropertyName("watch_signals")]
    public string[] WatchSignals { get; set; } = [];

    [JsonPropertyName("watch_constraints")]
    public int[] WatchConstraints { get; set; } = [];

    /// <summary>Passthrough for any extra keys (def, import, etc.) from MODULE creation.</summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Extra { get; set; }
}
