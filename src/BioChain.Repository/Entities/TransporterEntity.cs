namespace BioChain.Repository.Entities;

public class TransporterEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int? SignalId { get; set; }
    public string? SignalCode { get; set; }
    public string? SignalType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = "active";
    public string Clearance { get; set; } = "≈";
    public int? ModuleId { get; set; }
    public string? Cause { get; set; }
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public SignalEntity? Signal { get; set; }
    public ModuleEntity? Module { get; set; }
    public AnalysisEntity? Analysis { get; set; }
}
