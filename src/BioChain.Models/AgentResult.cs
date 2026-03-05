namespace BioChain.Models;

public sealed record AgentResult(
    string AgentName,
    string? Layer,
    string RawResponse,
    bool Success);
