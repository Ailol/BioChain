namespace NeuroGateway.Repository.Entities;

public class AnalysisRunEntity
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public int AnalysisTypeId { get; set; }
    public string Status { get; set; } = "pending";  // pending, running, completed, failed
    public string? TriggeredBy { get; set; }
    public Guid? ParentRunId { get; set; }
    public int[]? InputDataIds { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public string? Summary { get; set; }             // JSONB
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
