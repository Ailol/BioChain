namespace NeuroGateway.Repository.Entities;

public class LayerEntity
{
    public int Id { get; set; }
    public int PipelineId { get; set; }
    public string Name { get; set; } = "";
    public int AgentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsSynthesizer { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Config { get; set; } = "{}";
}
