namespace NeuroGateway.Repository.Entities;

public class AnalysisTypeEntity
{
    public int Id { get; set; }
    public int? DomainId { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int Version { get; set; } = 1;
    public int[]? DependsOn { get; set; }            // analysis_type IDs that must complete first
    public int SortOrder { get; set; }
    public string Config { get; set; } = "{}";       // agent_instructions, signals_to_detect, output_spec
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
