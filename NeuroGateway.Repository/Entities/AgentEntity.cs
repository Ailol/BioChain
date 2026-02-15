namespace NeuroGateway.Repository.Entities;

public class AgentEntity
{
    public int Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid? PersonId { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string[] Responsibilities { get; set; } = [];
    public string Style { get; set; } = "";
    public int MaxWords { get; set; } = 200;
    public bool IsSynthesizer { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
