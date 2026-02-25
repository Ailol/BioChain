namespace BioChain.Repository.Entities;

public class GateInstanceEntity
{
    public int Id { get; set; }
    public int GateId { get; set; }
    public string? Name { get; set; }                // 'nmda_coincidence', 'hpa_threshold'
    public string Formula { get; set; } = "";        // {⊼: GLU.bind, depolarization, GLY → NMDA.activate}
    public int[]? InputSignals { get; set; }
    public int? OutputSignalId { get; set; }
    public int? ModulatorSignalId { get; set; }
    public string? ThresholdValue { get; set; }      // '>20μg/dL', '⊨(high)'
    public int? RegionId { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
