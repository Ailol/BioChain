using Pgvector;

namespace BioChain.Repository.Entities;

public class GateEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? ModuleId { get; set; }
    public string? Threshold { get; set; }
    public string? Expression { get; set; }
    public decimal? Probability { get; set; }
    public int? ParentId { get; set; }
    public bool Latched { get; set; }
    public string[]? History { get; set; }
    // LLM_GATE fields
    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public string? ParseMap { get; set; }
    public string? FallbackExpr { get; set; }
    public int? TimeoutMs { get; set; }
    public int? CacheMs { get; set; }
    //
    public string? Cause { get; set; }
    public Vector? Embedding { get; set; }
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public ModuleEntity? Module { get; set; }
    public GateEntity? Parent { get; set; }
    public AnalysisEntity? Analysis { get; set; }
    public ICollection<GateEntity> Children { get; set; } = [];
}
