using System.Text.Json.Serialization;

namespace NeuroGateway.Models;

/// <summary>
/// A relationship type (dating, friend, coworker, etc.) from the DB seed data.
/// </summary>
public record RelationshipType(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description
);
