namespace BioChain.Repository.Entities;

public class PersonShareEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string? SharedWithUserId { get; set; }
    public string? SharedByUserId { get; set; }
    public string? SharedWithEmail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public PersonEntity Person { get; set; } = null!;
}
