namespace BioChain.Repository.Entities;

public class AnalysisDimensionEntity
{
    public int Id { get; set; }
    public int AnalysisTypeId { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int[]? TargetSignals { get; set; }
    public int[]? TargetRegions { get; set; }
    public string OutputType { get; set; } = "state";
    public string Config { get; set; } = "{}";
    public int SortOrder { get; set; }
}
