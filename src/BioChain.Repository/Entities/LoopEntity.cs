namespace BioChain.Repository.Entities;

public class LoopEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int ModuleId { get; set; }
    public string Polarity { get; set; } = string.Empty;
    public string? Subtype { get; set; }
    public decimal? GainProduct { get; set; }
    public long? TimeConstantMs { get; set; }
    public bool Active { get; set; } = true;
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public ModuleEntity Module { get; set; } = null!;
    public AnalysisEntity? Analysis { get; set; }
    public ICollection<EdgeEntity> Edges { get; set; } = [];
}
