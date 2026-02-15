using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class BiochemicalProfileEntity
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int? AnalyzedDataId { get; set; }
    public string Chemical { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public Vector? Embedding { get; set; }
    public float ModulationFactor { get; set; }
    public DateTime CreatedAt { get; set; }
}
