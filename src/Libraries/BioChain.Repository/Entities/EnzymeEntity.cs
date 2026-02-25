namespace BioChain.Repository.Entities;

public class EnzymeEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = "";            // TH, AADC, MAO-A, MAO-B, COMT, TPH2, IDO, FAAH
    public string Label { get; set; } = "";
    public string Function { get; set; } = "";       // synthesis, degradation, conversion, shunt
    public int? SubstrateSignalId { get; set; }
    public int? ProductSignalId { get; set; }
    public bool IsRateLimiting { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
