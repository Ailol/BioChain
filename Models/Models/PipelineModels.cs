using System.Text.Json.Serialization;

namespace Models;

public record PipelineInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("personId")] Guid PersonId,
    [property: JsonPropertyName("relationshipType")] string? RelationshipType,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("layers")] List<LayerInfo> Layers
);

public record LayerInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("agentId")] int AgentId,
    [property: JsonPropertyName("agentName")] string AgentName,
    [property: JsonPropertyName("sortOrder")] int SortOrder,
    [property: JsonPropertyName("isSynthesizer")] bool IsSynthesizer
);
