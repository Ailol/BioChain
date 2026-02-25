namespace BioChain.Repository.Entities;

public class CircuitPathwayEntity
{
    public int Id { get; set; }
    public int CircuitId { get; set; }
    public int PathwayId { get; set; }
    public string? Role { get; set; }                // primary, modulatory, feedback, opponent
    public string Config { get; set; } = "{}";
}
