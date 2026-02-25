namespace NeuroGateway.Repository.Entities;

public class PathwayStepEntity
{
    public int Id { get; set; }
    public int PathwayId { get; set; }
    public int StepOrder { get; set; }
    public int? SignalId { get; set; }
    public int? RegionId { get; set; }
    public int? ReceptorId { get; set; }
    public int? EnzymeId { get; set; }
    public int? GateInstanceId { get; set; }
    public string ConnectionType { get; set; } = ""; // excitatory, inhibitory, modulatory, gated, blocked
    public string? Formula { get; set; }
    public string Config { get; set; } = "{}";
}
