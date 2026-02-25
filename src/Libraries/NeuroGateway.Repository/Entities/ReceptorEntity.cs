namespace NeuroGateway.Repository.Entities;

public class ReceptorEntity
{
    public int Id { get; set; }
    public int SignalId { get; set; }
    public string Key { get; set; } = "";            // D1, D2, 5HT.1A, GABA.A, mu_OR, CB1
    public string Label { get; set; } = "";
    public string? Subtype { get; set; }
    public string? GProtein { get; set; }            // Gs, Gi, Gq, ion, nuclear, beta-arr
    public string? IonChannel { get; set; }          // Cl-, Na+, Ca2+, K+
    public string? Location { get; set; }            // presynaptic, postsynaptic, somatodendritic, auto
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
