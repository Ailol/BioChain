namespace BioChain.Repository.Entities;

public class DimensionSignalAffinityEntity
{
    public int Id { get; set; }
    public int DimensionId { get; set; }
    public int SignalId { get; set; }
    public float Weight { get; set; }
    public string Config { get; set; } = "{}";
}
