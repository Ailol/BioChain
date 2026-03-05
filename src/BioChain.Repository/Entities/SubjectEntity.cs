using Pgvector;

namespace BioChain.Repository.Entities;

public class SubjectEntity
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "person";
    public string? Meta { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public ICollection<StimuliEntity> Stimuli { get; set; } = [];
    public ICollection<ModuleEntity> Modules { get; set; } = [];
    public ICollection<SignalEntity> Signals { get; set; } = [];
    public ICollection<ReceptorEntity> Receptors { get; set; } = [];
    public ICollection<TransporterEntity> Transporters { get; set; } = [];
    public ICollection<GateEntity> Gates { get; set; } = [];
    public ICollection<LimiterEntity> Limiters { get; set; } = [];
    public ICollection<InterfaceEntity> Interfaces { get; set; } = [];
    public ICollection<RegionEntity> Regions { get; set; } = [];
    public ICollection<LoopEntity> Loops { get; set; } = [];
    public ICollection<PlasticityEntity> Plasticities { get; set; } = [];
    public ICollection<PathwayEntity> Pathways { get; set; } = [];
    public ICollection<EdgeEntity> Edges { get; set; } = [];
    public ICollection<ConstraintDefEntity> Constraints { get; set; } = [];
    public ICollection<ToolEntity> Tools { get; set; } = [];
    public ICollection<PersonShareEntity> Shares { get; set; } = [];
    public ICollection<QuestionnaireEntity> Questionnaires { get; set; } = [];
}
