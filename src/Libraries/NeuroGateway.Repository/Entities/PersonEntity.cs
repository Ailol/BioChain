namespace NeuroGateway.Repository.Entities;

public class PersonEntity
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Ssn { get; set; }
    public DateOnly? Birthdate { get; set; }
    public string? Address { get; set; }
    public string? Postcode { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }
}
