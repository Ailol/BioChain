using Pgvector;

namespace BioChain.Repository.Entities;

public class SignalEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string State { get; set; } = string.Empty;
    public string? Baseline { get; set; }
    public string? TauMin { get; set; }
    public string? TauMax { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }
    public DateTimeOffset UpdatedOnUtc { get; set; }

    public PersonEntity Person { get; set; } = null!;
    public ICollection<ReceptorEntity> Receptors { get; set; } = [];
    public ICollection<TransporterEntity> Transporters { get; set; } = [];
    public ICollection<LimiterEntity> Limiters { get; set; } = [];
}
