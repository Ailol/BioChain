namespace BioChain.Repository.Entities;

public class ToolEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Invoke { get; set; } = string.Empty;
    public string[] InputRefs { get; set; } = [];
    public string[] OutputRefs { get; set; } = [];
    public string? GateExpr { get; set; }
    public int TimeoutMs { get; set; } = 10000;
    public int RetryCount { get; set; }
    public string? Fallback { get; set; }
    public int? ModuleId { get; set; }
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public ModuleEntity? Module { get; set; }
    public AnalysisEntity? Analysis { get; set; }
}
