namespace NeuroGateway.Repository.Entities;

public class PersonShareEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string SharedWithEmail { get; set; } = "";
    public string? SharedWithUserId { get; set; }
    public string SharedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
