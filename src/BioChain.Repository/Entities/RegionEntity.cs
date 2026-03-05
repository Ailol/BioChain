using Pgvector;

namespace BioChain.Repository.Entities;

public class RegionEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? System { get; set; }
    public int? ParentId { get; set; }
    public int? ModuleId { get; set; }
    public string ActivityState { get; set; } = "unknown";
    public string? DominantSignal { get; set; }
    public string StressLoad { get; set; } = "≈";
    public string? Properties { get; set; }
    public string? Cause { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public RegionEntity? Parent { get; set; }
    public ModuleEntity? Module { get; set; }
    public ICollection<RegionEntity> Children { get; set; } = [];
    public ICollection<SignalEntity> Signals { get; set; } = [];
    public ICollection<InterfaceEntity> SourceInterfaces { get; set; } = [];
    public ICollection<InterfaceEntity> TargetInterfaces { get; set; } = [];
    public ICollection<PathwayEntity> SourcePathways { get; set; } = [];
    public ICollection<PathwayEntity> TargetPathways { get; set; } = [];
}
