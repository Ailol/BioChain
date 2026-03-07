namespace BioChain.Repository.Entities;

public class PathwayEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public int ModuleId { get; set; }
    public int? SourceRegionId { get; set; }
    public int? TargetRegionId { get; set; }
    public string? Expression { get; set; }
    public bool Active { get; set; } = true;
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public ModuleEntity Module { get; set; } = null!;
    public RegionEntity? SourceRegion { get; set; }
    public RegionEntity? TargetRegion { get; set; }
    public AnalysisEntity? Analysis { get; set; }
    public ICollection<InterfaceEntity> Interfaces { get; set; } = [];
    public ICollection<EdgeEntity> Edges { get; set; } = [];
}
