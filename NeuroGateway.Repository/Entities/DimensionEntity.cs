namespace NeuroGateway.Repository.Entities;

public class DimensionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Section { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public float WorkRelevance { get; set; } = 1.0f;
    public float PrivateRelevance { get; set; } = 1.0f;
    public string? ArchetypeName { get; set; }
    public string? ArchetypeEssence { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
