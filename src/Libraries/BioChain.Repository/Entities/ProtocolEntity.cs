using Pgvector;

namespace BioChain.Repository.Entities;

public class ProtocolEntity
{
    public int Id { get; set; }
    public Guid? PersonId { get; set; }
    public string? Tag { get; set; }
    public string Formula { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Phase { get; set; }
    public int? DataId { get; set; }
    public int? SignalSourceId { get; set; }
    public int? SignalTargetId { get; set; }
    public int? ReceptorId { get; set; }
    public int? TransporterId { get; set; }
    public int? GateId { get; set; }
    public int? LimiterId { get; set; }
    public int? InterfaceId { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }
    public DateTimeOffset UpdatedOnUtc { get; set; }

    public PersonEntity? Person { get; set; }
    public DataEntity? Data { get; set; }
    public SignalEntity? SignalSource { get; set; }
    public SignalEntity? SignalTarget { get; set; }
    public ReceptorEntity? Receptor { get; set; }
    public TransporterEntity? Transporter { get; set; }
    public GateEntity? Gate { get; set; }
    public LimiterEntity? Limiter { get; set; }
    public InterfaceEntity? Interface { get; set; }
}
