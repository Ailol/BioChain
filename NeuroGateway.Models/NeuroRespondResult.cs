namespace NeuroGateway.Models;

public sealed record ChatRespondResult(
    List<AnalysisDecision> Decisions,
    string Synthesis,
    Dictionary<string, string> LayerResponses,
    string SuggestedResponse);

public sealed record AnalysisResult(
    List<AnalysisDecision> Decisions,
    string Synthesis);
