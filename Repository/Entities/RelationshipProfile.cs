using Pgvector;

namespace Repository.Entities;

public class RelationshipProfile
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int RelationshipTypeId { get; set; }
    public Vector? CompatibilityVector { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Person Person { get; set; } = null!;
    public RelationshipType RelationshipType { get; set; } = null!;
}
