using Pgvector;

namespace BioChain.Repository.Entities;

public class AnalysisEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public int? StimuliId { get; set; }
    public int? ModuleId { get; set; }
    public string? Tag { get; set; }
    public string Formula { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Phase { get; set; }
    public int? Seq { get; set; }
    public string? BindExpr { get; set; }
    public string? FailCondition { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public StimuliEntity? Stimuli { get; set; }
    public ModuleEntity? Module { get; set; }
}
