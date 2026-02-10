using System.Text.Json.Serialization;

namespace NeuroGateway.Models;

public record CustomAgentGroup(
    Guid Id,
    string? PersonName,
    string GroupName,
    DateTime CreatedAt,
    int AgentCount,
    List<string> AgentNames
);

public record CustomAgent(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("responsibilities")] List<string> Responsibilities,
    [property: JsonPropertyName("style")] string Style,
    [property: JsonPropertyName("maxWords")] int MaxWords,
    [property: JsonPropertyName("isSynthesizer")] bool IsSynthesizer = false
);

public record CustomAgentGroupDetail(
    Guid Id,
    string? PersonName,
    string GroupName,
    DateTime CreatedAt,
    List<CustomAgent> Agents
);
