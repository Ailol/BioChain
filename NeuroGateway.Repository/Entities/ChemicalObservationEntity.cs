using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class ChemicalObservationEntity
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int? AnalyzedDataId { get; set; }
    public string Chemical { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public Vector? Embedding { get; set; }
    public float IntensityFactor { get; set; }
    public DateTime CreatedAt { get; set; }
}
