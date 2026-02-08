using System.Text.Json.Serialization;

namespace Models;

public record Trait([property: JsonPropertyName("topic")] string Topic, [property: JsonPropertyName("explanation")] string Explanation, string? Neurotransmitter = null);
