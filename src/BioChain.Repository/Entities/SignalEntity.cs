namespace BioChain.Repository.Entities;

public class SignalEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? RegionId { get; set; }
    public int? ModuleId { get; set; }
    public string State { get; set; } = "\u2248";
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public decimal? Baseline { get; set; }
    public decimal? DeviationPct { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public decimal Confidence { get; set; } = 1.0m;
    public string? Distribution { get; set; }
    public long? TauMinMs { get; set; }
    public long? TauMaxMs { get; set; }
    public string? Trend { get; set; }
    public string? Cause { get; set; }
    public int? ProtocolId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public RegionEntity? Region { get; set; }
    public ModuleEntity? Module { get; set; }
    public ProtocolEntity? Protocol { get; set; }
    public ICollection<ReceptorEntity> Receptors { get; set; } = [];
    public ICollection<TransporterEntity> Transporters { get; set; } = [];
    public ICollection<LimiterEntity> Limiters { get; set; } = [];
}
