using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class EmbeddingCacheEntity
{
    public int Id { get; set; }
    public string CacheType { get; set; } = "";
    public int? DomainId { get; set; }
    public string LookupKey { get; set; } = "";
    public string? Label { get; set; }
    public Vector Embedding { get; set; } = null!;
    public string Metadata { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
