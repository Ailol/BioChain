namespace NeuroGateway.Models;

public sealed record NeuroRespondResult(
    List<AnalysisDecision> Decisions,
    string Synthesis,
    Dictionary<string, string> LayerResponses,
    string SuggestedResponse);
