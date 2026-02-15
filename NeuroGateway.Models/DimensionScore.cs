namespace NeuroGateway.Models;

public sealed record DimensionScore(
    string Name,
    string Category,
    int Score,
    float Coherence,
    int EvidenceCount,
    List<DimensionEvidence> Evidence);

public sealed record DimensionEvidence(
    string Chemical,
    string Layer,
    string Reasoning,
    float Similarity,
    float Recency);
