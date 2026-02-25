namespace NeuroGateway.Repository.Entities;

public class SignalEntity
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Layer { get; set; } = "";          // NT, H, P, NI, NS, eCB, behavior, metric
    public string Code { get; set; } = "";           // NT:DA, H:CORT, P:OXT, BEH:SOC
    public string? Unit { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
