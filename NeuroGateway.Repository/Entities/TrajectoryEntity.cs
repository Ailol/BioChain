namespace NeuroGateway.Repository.Entities;

public class TrajectoryEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int PersonalityId { get; set; }
    public int? DomainId { get; set; }
    public int? CircuitId { get; set; }
    public string Name { get; set; } = "";
    public string? TrajectoryType { get; set; }
    public string Status { get; set; } = "active";
    public string Config { get; set; } = "{}";
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
