namespace Models;

/// <summary>
/// Unified decision from any biochemical agent (NT, hormone, or peptide).
/// Each agent independently decides ADD or SKIP (presence-based, no strength float).
/// </summary>
public record BiochemicalDecision(string Chemical, string Reasoning);
