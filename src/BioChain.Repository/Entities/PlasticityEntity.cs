namespace BioChain.Repository.Entities;

public class PlasticityEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int? EdgeId { get; set; }
    public int? ReceptorId { get; set; }
    public string PlasticityType { get; set; } = string.Empty;
    public string? Timescale { get; set; }
    public int? InductionId { get; set; }
    public bool Consolidation { get; set; }
    public bool Reversible { get; set; } = true;
    public int? AnalysisId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public EdgeEntity? Edge { get; set; }
    public ReceptorEntity? Receptor { get; set; }
    public SignalEntity? Induction { get; set; }
    public AnalysisEntity? Analysis { get; set; }
}
