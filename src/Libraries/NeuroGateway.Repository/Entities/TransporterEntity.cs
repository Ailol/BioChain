namespace NeuroGateway.Repository.Entities;

public class TransporterEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = "";            // DAT, SERT, NET, GAT, VMAT2, EAAT, ChT
    public string Label { get; set; } = "";
    public int SignalId { get; set; }
    public string TransportType { get; set; } = "";  // reuptake, vesicular, clearance
    public string? Location { get; set; }            // presynaptic, astrocyte, vesicular
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
