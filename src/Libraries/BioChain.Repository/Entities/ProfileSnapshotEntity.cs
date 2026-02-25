namespace BioChain.Repository.Entities;

public class ProfileSnapshotEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int PersonalityId { get; set; }
    public int SignalId { get; set; }
    public string? LatestState { get; set; }
    public float? LatestIntensity { get; set; }
    public string? LatestFailureMode { get; set; }
    public int? LatestRegionId { get; set; }
    public string? LatestTemporal { get; set; }
    public string? LatestConfidence { get; set; }
    public string? LatestDoseRange { get; set; }
    public string? PreviousState { get; set; }
    public string? Trend { get; set; }               // improving, stable, declining, volatile
    public int ObservationCount { get; set; }
    public int? LastObservationId { get; set; }
    public DateTime? LastObservedAt { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
