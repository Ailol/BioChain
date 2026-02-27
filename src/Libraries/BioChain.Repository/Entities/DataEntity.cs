using Pgvector;

namespace BioChain.Repository.Entities;

public class DataEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? SourceText { get; set; }
    public string? Formula { get; set; }
    public bool Analyzed { get; set; }
    public string? Content { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public PersonEntity Person { get; set; } = null!;
}
