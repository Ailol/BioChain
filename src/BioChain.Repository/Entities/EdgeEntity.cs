using Pgvector;

namespace BioChain.Repository.Entities;

public class EdgeEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public int? TargetId { get; set; }

    // Code-based endpoints (store LLM codes directly)
    public string? SourceCode { get; set; }
    public string? SourceSignalType { get; set; }
    public string? SourceRegion { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetSignalType { get; set; }
    public string? TargetRegion { get; set; }
    public string? RelationshipKind { get; set; }

    // Gate code-based (alongside GateId FK)
    public string? GateCode { get; set; }
    public string? GateType { get; set; }
    public string? GateCondition { get; set; }

    public string Operator { get; set; } = string.Empty;
    public string OperatorClass { get; set; } = string.Empty;
    public string? Properties { get; set; }
    public decimal? Gain { get; set; }
    public decimal? NoiseSigma { get; set; }
    public string? TransferFn { get; set; }
    public long? DelayMs { get; set; }
    public decimal? ClampLo { get; set; }
    public decimal? ClampHi { get; set; }
    public int? GateId { get; set; }
    public int? LoopId { get; set; }
    public int? PathwayId { get; set; }
    public string? DysregType { get; set; }
    public int? ModuleId { get; set; }
    public int? ToolId { get; set; }
    public Vector? Embedding { get; set; }
    public int? AnalysisId { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public GateEntity? Gate { get; set; }
    public LoopEntity? Loop { get; set; }
    public PathwayEntity? Pathway { get; set; }
    public ModuleEntity? Module { get; set; }
    public ToolEntity? Tool { get; set; }
    public AnalysisEntity? Analysis { get; set; }
}
