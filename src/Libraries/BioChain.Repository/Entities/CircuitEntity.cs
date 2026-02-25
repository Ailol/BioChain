namespace BioChain.Repository.Entities;

public class CircuitEntity
{
    public int Id { get; set; }
    public int? DomainId { get; set; }
    public string Key { get; set; } = "";            // 'stress_adaptation_failure', 'reward_learning'
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? TriggerDescription { get; set; }
    public string? CompactFormula { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
