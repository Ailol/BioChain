namespace BioChain.Repository.Entities;

public class ReceptorEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int SignalId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Subtype { get; set; }
    public string State { get; set; } = "active";
    public int? ModuleId { get; set; }
    public string? Cause { get; set; }
    public int? ProtocolId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public SignalEntity Signal { get; set; } = null!;
    public ModuleEntity? Module { get; set; }
    public ProtocolEntity? Protocol { get; set; }
}
