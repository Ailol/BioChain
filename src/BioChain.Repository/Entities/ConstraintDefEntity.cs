namespace BioChain.Repository.Entities;

public class ConstraintDefEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public decimal? Epsilon { get; set; }
    public decimal Confidence { get; set; } = 1.0m;
    public int? ModuleId { get; set; }
    public bool Active { get; set; } = true;
    public int? ProtocolId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public ModuleEntity? Module { get; set; }
    public ProtocolEntity? Protocol { get; set; }
}
