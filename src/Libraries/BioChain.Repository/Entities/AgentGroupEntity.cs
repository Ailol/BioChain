namespace BioChain.Repository.Entities;

public class AgentGroupEntity
{
    public Guid Id { get; set; }
    public Guid? PersonId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<AgentEntity> Agents { get; set; } = [];
}
