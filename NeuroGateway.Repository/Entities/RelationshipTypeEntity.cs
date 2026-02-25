namespace NeuroGateway.Repository.Entities;

public class RelationshipTypeEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Config { get; set; } = "{}";
}
