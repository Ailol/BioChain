namespace BioChain.Repository.Entities;

public class EdgeEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }
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
    public int? ProtocolId { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public GateEntity? Gate { get; set; }
    public LoopEntity? Loop { get; set; }
    public PathwayEntity? Pathway { get; set; }
    public ModuleEntity? Module { get; set; }
    public ToolEntity? Tool { get; set; }
    public ProtocolEntity? Protocol { get; set; }
}
