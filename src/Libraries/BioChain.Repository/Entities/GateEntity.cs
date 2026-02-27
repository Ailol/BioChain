using Pgvector;

namespace BioChain.Repository.Entities;

public class GateEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Threshold { get; set; }
    public string? Expression { get; set; }
    public int? ParentId { get; set; }
    public string[]? History { get; set; }
    public bool Latched { get; set; }
    public Vector? Embedding { get; set; }

    public PersonEntity Person { get; set; } = null!;
    public GateEntity? Parent { get; set; }
    public ICollection<GateEntity> Children { get; set; } = [];
}
