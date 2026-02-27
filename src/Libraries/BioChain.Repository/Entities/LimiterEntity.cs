using Pgvector;

namespace BioChain.Repository.Entities;

public class LimiterEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int? TargetId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Reaction { get; set; }
    public bool RateLimiting { get; set; }
    public string? Activity { get; set; }
    public Vector? Embedding { get; set; }

    public PersonEntity Person { get; set; } = null!;
    public SignalEntity? Target { get; set; }
}
