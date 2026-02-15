namespace NeuroGateway.Repository.Entities;

public class PersonalityEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string? CommunicationStyle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
