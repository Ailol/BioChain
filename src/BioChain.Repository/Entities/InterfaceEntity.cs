namespace BioChain.Repository.Entities;

public class InterfaceEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int SourceRegionId { get; set; }
    public int TargetRegionId { get; set; }
    public string? Pathway { get; set; }
    public int? PathwayId { get; set; }
    public bool Active { get; set; } = true;
    public int? ModuleId { get; set; }
    public string? Cause { get; set; }
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public RegionEntity SourceRegion { get; set; } = null!;
    public RegionEntity TargetRegion { get; set; } = null!;
    public PathwayEntity? PathwayRef { get; set; }
    public ModuleEntity? Module { get; set; }
    public AnalysisEntity? Analysis { get; set; }
}
