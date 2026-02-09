using System.Text.Json.Serialization;

namespace Models;

/// <summary>
/// Result of querying a person's neuroresponse to a given text/situation.
/// Shows how much each neurotransmitter would be involved based on trait similarity.
/// </summary>
public record NeuroresponseResult(
    [property: JsonPropertyName("person")] string Person,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("neurotransmitterWeights")] List<NeurotransmitterWeight> NeurotransmitterWeights,
    [property: JsonPropertyName("topMatchingTraits")] List<MatchingTrait> TopMatchingTraits
);

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
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("neurotransmitter")] string Neurotransmitter,
    [property: JsonPropertyName("similarity")] double Similarity
);

/// <summary>
/// A trait extracted from raw text analysis by the LLM, with optional neurotransmitter suggestion.
/// </summary>
public record AnalyzedTrait(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("explanation")] string Explanation,
    [property: JsonPropertyName("suggestedNeurotransmitter")] string? SuggestedNeurotransmitter = null
);

/// <summary>
/// Structured neurorespond result for frontend consumption.
/// Contains neuroprofile data, per-agent analysis, and always 4 crafted responses.
/// </summary>
public record NeuroNarrativeResult(
    [property: JsonPropertyName("person")] string Person,
    [property: JsonPropertyName("theirMessage")] string TheirMessage,
    [property: JsonPropertyName("relationship")] string Relationship,
    [property: JsonPropertyName("neuroprofile")] NeuroprofileData Neuroprofile,
    [property: JsonPropertyName("agents")] Dictionary<string, string> Agents,
    [property: JsonPropertyName("analysis")] string Analysis,
    [property: JsonPropertyName("responses")] List<NeuroResponse> Responses
);

/// <summary>
/// A crafted response from a specific biochemical agent or the synthesizer.
/// </summary>
public record NeuroResponse(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("message")] string Message
);

/// <summary>
/// Raw neuroprofile data: weights, traits, hormones, peptides.
/// </summary>
public record NeuroprofileData(
    [property: JsonPropertyName("neurotransmitterWeights")] List<NeurotransmitterWeight> NeurotransmitterWeights,
    [property: JsonPropertyName("topMatchingTraits")] List<MatchingTrait> TopMatchingTraits,
    [property: JsonPropertyName("hormones")] List<HormoneScore> Hormones,
    [property: JsonPropertyName("peptides")] List<PeptideScore> Peptides
);

public record HormoneScore(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("traitCount")] int TraitCount
);

public record PeptideScore(
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
    MindHat,      // Analytical, philosophical, intellectual
    ExWife,       // Guarded, boundaries, formal distance
    Family,       // Warm but complex dynamics
    Colleague,    // Professional, work context
    Acquaintance  // Surface level, polite distance
}

