namespace NeuroGateway.Repository.Entities;

public class AgentGroup
{
    public Guid Id { get; set; }
    public Guid? PersonId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Person? Person { get; set; }
    public ICollection<Agent> Agents { get; set; } = [];
}
