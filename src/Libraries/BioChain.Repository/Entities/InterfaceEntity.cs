using Pgvector;

namespace BioChain.Repository.Entities;

public class InterfaceEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? SourceRegion { get; set; }
    public string? TargetRegion { get; set; }
    public string? Pathway { get; set; }
    public bool Active { get; set; }
    public Vector? Embedding { get; set; }

    public PersonEntity Person { get; set; } = null!;
}
