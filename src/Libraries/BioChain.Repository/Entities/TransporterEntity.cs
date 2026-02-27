using Pgvector;

namespace BioChain.Repository.Entities;

public class TransporterEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int SignalId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Clearance { get; set; }
    public Vector? Embedding { get; set; }

    public PersonEntity Person { get; set; } = null!;
    public SignalEntity Signal { get; set; } = null!;
}
