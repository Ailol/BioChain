using System.Text.Json.Serialization;

namespace Models;

/// <summary>
/// A relationship type (dating, friend, coworker, etc.) from the DB seed data.
/// </summary>
public record RelationshipType(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description
);

/// <summary>
/// Full relationship profile for a person + relationship type, including the compatibility vector.
/// </summary>
public record RelationshipProfile(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("person")] string Person,
    [property: JsonPropertyName("relationshipType")] string RelationshipType,
    [property: JsonPropertyName("compatibilityVector")] float[]? CompatibilityVector,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt
);

/// <summary>
/// A stale relationship profile — where personality traits have been updated more recently.
/// </summary>
public record StaleRelationshipProfile(
    [property: JsonPropertyName("relationshipType")] string RelationshipType,
    [property: JsonPropertyName("profileUpdatedAt")] DateTime ProfileUpdatedAt,
    [property: JsonPropertyName("latestTraitUpdate")] DateTime LatestTraitUpdate
);

/// <summary>
/// Summary of a relationship profile for listing (without the full vector).
/// </summary>
public record RelationshipProfileSummary(
    [property: JsonPropertyName("relationshipType")] string RelationshipType,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("hasVector")] bool HasVector
);
