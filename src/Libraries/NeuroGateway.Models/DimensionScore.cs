namespace NeuroGateway.Models;

public sealed record DimensionScore(
    string Name,
    string Section,
    string Category,
    int Score,
    float Confidence,
    float Consistency,
    int EvidenceCount,
    List<DimensionEvidence> Evidence,
    TemporalTrajectory? Trajectory = null,
    CircuitCoherence? Circuit = null);

public sealed record DimensionEvidence(
    string Signal,
    string Layer,
    string Formula,
    float Level,
    float Recency);

/// <summary>
/// Temporal trajectory: linear trend of activation levels over time.
/// Slope is level-change per day; direction summarizes the trend.
/// </summary>
public sealed record TemporalTrajectory(
    float Slope,
    string Direction,
    float R2,
    int DataPoints,
    float EarliestLevel,
    float LatestLevel,
    bool SemanticDriftDetected = false,
    float DriftMagnitude = 0f);

/// <summary>
/// Signal interaction graph for a dimension.
/// Shows which signals co-activate (positive edges) vs conflict (negative edges).
/// CircuitCoherenceScore measures how consistently the signal network agrees.
/// </summary>
public sealed record CircuitCoherence(
    float CoherenceScore,
    List<SignalEdge> Edges,
    string Pattern,
    List<string>? FailureModes = null);

public sealed record SignalEdge(
    string SignalA,
    string SignalB,
    float Correlation,
    string Relationship,
    float? KnownModFactor = null,
    string? KnownMechanism = null);

// Input data for the scoring algorithm: one signal observation with its embedding.
// Maps from ObservationRepository.ObservationEntry to decouple algorithm from persistence.
public sealed record SignalObservation(
    string Signal,
    string Formula,
    string? State,
    string? Circuits,
    float[] Embedding,
    float IntensityFactor,
    DateTime CreatedAt);

/// <summary>
/// One cell of the shadow level matrix: a single signal × dimension pair.
/// ShadowLevel is embedding-only (not averaged with intensity_factor).
/// </summary>
public sealed record ShadowMatrixCell(
    string Dimension,
    string Section,
    string Signal,
    string Layer,
    float ShadowLevel,
    float Confidence,
    int EntryCount);

/// <summary>
/// Full shadow matrix: sparse grid of signal × dimension shadow levels.
/// Dimensions and Signals lists provide ordered axis labels.
/// </summary>
public sealed record ShadowMatrixResponse(
    string Person,
    string Mode,
    List<ShadowMatrixCell> Cells,
    List<string> Dimensions,
    List<string> Signals);
