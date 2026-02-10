using System.Text.Json.Serialization;

namespace NeuroGateway.Models;

/// <summary>
/// Weight/involvement level for a neurotransmitter in responding to a query.
/// </summary>
public record NeurotransmitterWeight(
    [property: JsonPropertyName("neurotransmitter")] string Neurotransmitter,
    [property: JsonPropertyName("weight")] double Weight,
    [property: JsonPropertyName("traitCount")] int TraitCount
);

/// <summary>
/// A trait that matched the query with its similarity score.
/// </summary>
public record MatchingTrait(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("neurotransmitter")] string Neurotransmitter,
    [property: JsonPropertyName("similarity")] double Similarity
);

/// <summary>
/// Structured neurorespond result: person context + crafted responses (always 4: NT, hormone, peptide, synthesizer).
/// </summary>
public record NeuroNarrativeResult(
    [property: JsonPropertyName("person")] string Person,
    [property: JsonPropertyName("theirMessage")] string TheirMessage,
    [property: JsonPropertyName("relationship")] string Relationship,
    [property: JsonPropertyName("responses")] List<NeuroResponse> Responses,
    [property: JsonPropertyName("estimatedRelationship")] string? EstimatedRelationship = null
);

/// <summary>
/// A crafted response from a specific biochemical agent or the synthesizer.
/// HERE: where the relationship currently is (from this layer's perspective).
/// SHIFT: behavioral recommendation for the user.
/// Message: the suggested text to send (SUGGEST).
/// EstimatedRelationship: this layer's independent relationship estimate.
/// </summary>
public record NeuroResponse(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("here")] string? Here = null,
    [property: JsonPropertyName("shift")] string? Shift = null,
    [property: JsonPropertyName("estimatedRelationship")] string? EstimatedRelationship = null
);

public record ChemicalScore(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("traitCount")] int TraitCount
);

/// <summary>
/// Result of the backfill embeddings operation.
/// </summary>
public record BackfillResult(
    [property: JsonPropertyName("updatedCount")] int UpdatedCount,
    [property: JsonPropertyName("skippedCount")] int SkippedCount,
    [property: JsonPropertyName("errorCount")] int ErrorCount,
    [property: JsonPropertyName("message")] string Message
);

/// <summary>
/// Responder group classification for context-aware responses.
/// Each group has specialized neurotransmitter agents that craft responses.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResponderGroup
{
    Dating,       // Excitement, anticipation, romantic interest
    Relationship, // Stability, commitment, deep connection
    Friend,       // Casual, balanced, social bonding
    Mindhat,      // Analytical, philosophical, intellectual
    Exwife,       // Guarded, boundaries, formal distance
    Family,       // Warm but complex dynamics
    Colleague,    // Professional, work context
    Acquaintance, // Surface level, polite distance
    Partner,      // Intimate, secure, emotionally attuned
    Conflict      // Calm, de-escalating, boundaried
}

