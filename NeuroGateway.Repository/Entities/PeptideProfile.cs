using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class PeptideProfile
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int PeptideId { get; set; }
    public int? AnalyzedDataId { get; set; }
    public string? Reasoning { get; set; }
    public Vector? ReasoningEmbedding { get; set; }
    public int? ClusterId { get; set; }
    public bool IsClusterRepresentative { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Personality Personality { get; set; } = null!;
    public Peptide Peptide { get; set; } = null!;
    public AnalyzedData? AnalyzedData { get; set; }
}
