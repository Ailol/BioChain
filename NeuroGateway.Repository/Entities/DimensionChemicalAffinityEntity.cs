namespace NeuroGateway.Repository.Entities;

public class DimensionChemicalAffinityEntity
{
    public int Id { get; set; }
    public int DimensionId { get; set; }
    public int ChemicalId { get; set; }
    public float Weight { get; set; }
}
