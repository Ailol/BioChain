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
    string Chemical,
    string Layer,
    string Reasoning,
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
    float LatestLevel);

/// <summary>
/// Chemical interaction graph for a dimension.
/// Shows which chemicals co-activate (positive edges) vs conflict (negative edges).
/// CircuitCoherenceScore measures how consistently the chemical network agrees.
/// </summary>
public sealed record CircuitCoherence(
    float CoherenceScore,
    List<ChemicalEdge> Edges,
    string Pattern);

public sealed record ChemicalEdge(
    string ChemicalA,
    string ChemicalB,
    float Correlation,
    string Relationship);

/// <summary>
/// One cell of the shadow level matrix: a single chemical × dimension pair.
/// ShadowLevel is embedding-only (not averaged with intensity_factor).
/// </summary>
public sealed record ShadowMatrixCell(
    string Dimension,
    string Section,
    string Chemical,
    string Layer,
    float ShadowLevel,
    float Confidence,
    int EntryCount);

/// <summary>
/// Full shadow matrix: sparse grid of chemical × dimension shadow levels.
/// Dimensions and Chemicals lists provide ordered axis labels.
/// </summary>
public sealed record ShadowMatrixResponse(
    string Person,
    string Mode,
    List<ShadowMatrixCell> Cells,
    List<string> Dimensions,
    List<string> Chemicals);
