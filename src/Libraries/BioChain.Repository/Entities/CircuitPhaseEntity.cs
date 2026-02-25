namespace BioChain.Repository.Entities;

public class CircuitPhaseEntity
{
    public int Id { get; set; }
    public int CircuitId { get; set; }
    public int PhaseOrder { get; set; }
    public string PhaseLabel { get; set; } = "";     // initiation, amplification, multi_target, feedback, failure
    public string? Temporal { get; set; }            // seconds-minutes, minutes, hours, weeks-months
    public string StateBlock { get; set; } = "";
    public string? Description { get; set; }
    public string Config { get; set; } = "{}";
}
