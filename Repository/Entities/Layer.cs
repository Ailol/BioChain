namespace Repository.Entities;

public class Layer
{
    public int Id { get; set; }
    public int PipelineId { get; set; }
    public string Name { get; set; } = "";
    public int AgentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsSynthesizer { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Pipeline Pipeline { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
