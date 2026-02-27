using Pgvector;

namespace BioChain.Repository.Entities;

public class PersonEntity
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Meta { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }
    public DateTimeOffset UpdatedOnUtc { get; set; }

    public ICollection<DataEntity> Events { get; set; } = [];
    public ICollection<SignalEntity> Signals { get; set; } = [];
    public ICollection<ReceptorEntity> Receptors { get; set; } = [];
    public ICollection<TransporterEntity> Transporters { get; set; } = [];
    public ICollection<GateEntity> Gates { get; set; } = [];
    public ICollection<LimiterEntity> Limiters { get; set; } = [];
    public ICollection<InterfaceEntity> Interfaces { get; set; } = [];
    public ICollection<PersonShareEntity> Shares { get; set; } = [];
    public ICollection<QuestionnaireEntity> Questionnaires { get; set; } = [];
}
