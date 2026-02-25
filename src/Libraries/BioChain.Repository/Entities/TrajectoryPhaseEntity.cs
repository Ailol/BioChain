using Pgvector;

namespace BioChain.Repository.Entities;

public class TrajectoryPhaseEntity
{
    public int Id { get; set; }
    public int TrajectoryId { get; set; }
    public int PhaseNumber { get; set; }
    public string? PhaseLabel { get; set; }
    public string StateSnapshot { get; set; } = "";
    public string? Summary { get; set; }
    public int[]? ObservationIds { get; set; }
    public Guid? AnalysisRunId { get; set; }
    public int? CircuitPhaseId { get; set; }
    public string Metadata { get; set; } = "{}";
    public Vector? StateEmbedding { get; set; }
    public DateTime? ObservedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
