namespace NeuroGateway.Repository.Entities;

public class EntityTagEntity
{
    public int Id { get; set; }
    public int TagId { get; set; }
    public string EntityType { get; set; } = "";     // observation, analysis_run, trajectory, loop, person
    public string EntityId { get; set; } = "";
    public string? Severity { get; set; }
    public string? Confidence { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
