namespace NeuroGateway.Repository.Entities;

public class Agent
{
    public int Id { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? PersonId { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public List<string> Responsibilities { get; set; } = [];
    public string Style { get; set; } = "";
    public int MaxWords { get; set; } = 200;
    public bool IsSynthesizer { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public AgentGroup? Group { get; set; }
    public Person? Person { get; set; }
}
