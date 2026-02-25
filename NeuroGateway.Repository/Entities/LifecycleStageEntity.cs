namespace NeuroGateway.Repository.Entities;

public class LifecycleStageEntity
{
    public int Id { get; set; }
    public int SignalId { get; set; }
    public string Stage { get; set; } = "";          // syn, pkg, trg, rel, bnd, txd, amp, eff, trm, fbk
    public int StageOrder { get; set; }
    public string Formula { get; set; } = "";
    public string? Description { get; set; }
    public int? RateLimitingEnzymeId { get; set; }
    public int? GateInstanceId { get; set; }
    public int? TransporterId { get; set; }
    public int[]? ReceptorIds { get; set; }
    public int? RegionId { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
