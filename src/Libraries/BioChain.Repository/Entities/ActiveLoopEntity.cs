using Pgvector;

namespace BioChain.Repository.Entities;

public class ActiveLoopEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int PersonalityId { get; set; }
    public int? DomainId { get; set; }
    public int? PathwayId { get; set; }
    public string Name { get; set; } = "";
    public string LoopType { get; set; } = "";       // NFB, PFB
    public string? Polarity { get; set; }            // virtuous, vicious, stabilizing, destabilizing
    public string Status { get; set; } = "";         // intact, degraded, broken, latched, emerging
    public string Formula { get; set; } = "";
    public int[] InvolvedSignals { get; set; } = [];
    public int[]? InvolvedGateIds { get; set; }
    public string? FailureMode { get; set; }
    public string? Severity { get; set; }
    public Guid? AnalysisRunId { get; set; }
    public string? Notes { get; set; }
    public string Metadata { get; set; } = "{}";
    public Vector? Embedding { get; set; }
    public DateTime? FirstDetectedAt { get; set; }
    public DateTime? LastConfirmedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
