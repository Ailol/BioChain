namespace BioChain.Models;

public sealed record AgentDefinition(
    string Name,
    string SystemPrompt,
    string? Layer = null);
