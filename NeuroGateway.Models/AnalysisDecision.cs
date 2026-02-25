namespace NeuroGateway.Models;

public sealed record AnalysisDecision(
    string Signal,          // signal key (dopamine, serotonin...)
    int SignalId,           // FK for storage
    string? Signals,        // SIGNALS section text
    string Formula,         // FORMULA section text (full notation formula)
    string? State,          // STATE section text
    string? Circuits,       // CIRCUITS section text
    // v6 structured fields parsed from formula:
    string? SubjectState,
    string? Operator,
    int? TargetSignalId,
    string? TargetState,
    int? RegionId,
    string? Temporal,
    string? Confidence,
    string? FailureMode,
    float Intensity);
