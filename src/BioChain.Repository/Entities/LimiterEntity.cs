using Pgvector;

namespace BioChain.Repository.Entities;

public class LimiterEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int? TargetId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Reaction { get; set; }
    public bool RateLimiting { get; set; }
    public string Activity { get; set; } = "≈";
    public int? ModuleId { get; set; }
    public string? Cause { get; set; }
    public Vector? Embedding { get; set; }
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public SignalEntity? Target { get; set; }
    public ModuleEntity? Module { get; set; }
    public AnalysisEntity? Analysis { get; set; }
}
