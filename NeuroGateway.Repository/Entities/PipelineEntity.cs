namespace NeuroGateway.Repository.Entities;

public class PipelineEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Guid PersonId { get; set; }
    public int? RelationshipTypeId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
