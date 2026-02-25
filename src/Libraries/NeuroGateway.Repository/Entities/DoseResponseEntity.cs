namespace NeuroGateway.Repository.Entities;

public class DoseResponseEntity
{
    public int Id { get; set; }
    public int SignalId { get; set; }
    public string Pattern { get; set; } = "";        // INVERTED_U, LINEAR, SIGMOID, BIPHASIC, U_SHAPED
    public string? LowEffect { get; set; }
    public string? OptimalEffect { get; set; }
    public string? HighEffect { get; set; }
    public string? ExcessEffect { get; set; }
    public int? RegionId { get; set; }
    public string? Context { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
