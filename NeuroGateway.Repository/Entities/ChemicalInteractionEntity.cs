namespace NeuroGateway.Repository.Entities;

public class ChemicalInteractionEntity
{
    public int Id { get; set; }
    public int SourceChemicalId { get; set; }
    public int TargetChemicalId { get; set; }
    public float ModFactor { get; set; }
    public string? Mechanism { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
