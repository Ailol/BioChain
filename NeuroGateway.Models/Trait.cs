using System.Text.Json.Serialization;

namespace NeuroGateway.Models;

/// <summary>
/// An analyzed input entry with its biochemical profile.
/// Replaces the old "Trait" model — now represents analyzed content, not a topic.
/// </summary>
public record AnalyzedEntry(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("sourceType")] string? SourceType = null,
    [property: JsonPropertyName("neurotransmitters")] List<string>? Neurotransmitters = null,
    [property: JsonPropertyName("hormones")] List<string>? Hormones = null,
    [property: JsonPropertyName("peptides")] List<string>? Peptides = null,
    [property: JsonPropertyName("analyzedDataId")] int? AnalyzedDataId = null)
{
    [JsonIgnore]
    public string PrimaryNt => Neurotransmitters?.FirstOrDefault() ?? "Unknown";

    public string AllChemicals()
    {
        var all = (Neurotransmitters ?? []).Concat(Hormones ?? []).Concat(Peptides ?? []).ToList();
        return all.Count > 0 ? string.Join(", ", all) : "general";
    }
}
