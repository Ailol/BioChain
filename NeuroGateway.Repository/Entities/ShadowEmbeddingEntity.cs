using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class ShadowEmbeddingEntity
{
    public int Id { get; set; }
    public string Dimension { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Chemical { get; set; } = "";
    public int Level { get; set; }
    public Vector Embedding { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
