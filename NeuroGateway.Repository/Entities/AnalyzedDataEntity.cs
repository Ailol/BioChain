using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class AnalyzedDataEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Content { get; set; } = "";
    public string? SourceType { get; set; }
    public string? SourceUri { get; set; }
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Metadata { get; set; } = "{}";
}
