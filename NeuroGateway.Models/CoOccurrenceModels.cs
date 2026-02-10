using System.Text.Json.Serialization;

namespace NeuroGateway.Models;

/// <summary>
/// Co-occurrence relationship between a source biochemical and a target biochemical.
/// SharedTraitCount = how many personality traits both chemicals appear on for this person.
/// </summary>
public record CoOccurrence(
    [property: JsonPropertyName("chemical")] string Chemical,
    [property: JsonPropertyName("sharedTraitCount")] int SharedTraitCount,
    [property: JsonPropertyName("exampleTraits")] List<string> ExampleTraits
);

/// <summary>
/// Full co-occurrence analysis for a single biochemical.
/// Shows which chemicals from other layers co-occur most frequently.
/// </summary>
public record CoOccurrenceProfile(
    [property: JsonPropertyName("sourceChemical")] string SourceChemical,
    [property: JsonPropertyName("sourceLayer")] string SourceLayer,
    [property: JsonPropertyName("coOccurringNeurotransmitters")] List<CoOccurrence>? CoOccurringNeurotransmitters,
    [property: JsonPropertyName("coOccurringHormones")] List<CoOccurrence>? CoOccurringHormones,
    [property: JsonPropertyName("coOccurringPeptides")] List<CoOccurrence>? CoOccurringPeptides
);
