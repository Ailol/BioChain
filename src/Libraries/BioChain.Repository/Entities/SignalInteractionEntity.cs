namespace BioChain.Repository.Entities;

public class SignalInteractionEntity
{
    public int Id { get; set; }
    public int SourceSignalId { get; set; }
    public int TargetSignalId { get; set; }
    public string Operator { get; set; } = "";       // →, ⊣, ⊃, ⊂, ⊩, ⇌, ∥, ⊗, ≫, ≂, ⊘→
    public float? ModFactor { get; set; }
    public string? Mechanism { get; set; }
    public int? ViaEnzymeId { get; set; }
    public int? ViaReceptorId { get; set; }
    public int? ViaTransporterId { get; set; }
    public int? RegionId { get; set; }
    public string? Temporal { get; set; }            // acute, chronic, tonic, phasic, pulsatile, permissive, circadian
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
