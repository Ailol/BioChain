namespace Repository.Entities;

public class RelationshipType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<Pipeline> Pipelines { get; set; } = [];
}
