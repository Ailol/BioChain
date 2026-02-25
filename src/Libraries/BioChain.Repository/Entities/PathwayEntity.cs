namespace BioChain.Repository.Entities;

public class PathwayEntity
{
    public int Id { get; set; }
    public int? DomainId { get; set; }
    public string Key { get; set; } = "";            // 'hpa_axis', 'mesolimbic_da', 'drn_5ht'
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? SourceRegionId { get; set; }
    public int? TargetRegionId { get; set; }
    public int? PrimarySignalId { get; set; }
    public string? CompactFormula { get; set; }
    public string? TemplateType { get; set; }        // linear_cascade, neg_feedback, disinhibition, etc.
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
