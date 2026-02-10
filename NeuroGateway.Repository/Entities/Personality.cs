namespace NeuroGateway.Repository.Entities;

/// <summary>
/// Thin 1:1 anchor per person. Personality = the full biochemical landscape.
/// Profiles ARE the personality — queried fresh at runtime.
/// </summary>
public class Personality
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string? CommunicationStyle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Person Person { get; set; } = null!;
    public ICollection<NeurotransmitterProfile> NeurotransmitterProfiles { get; set; } = [];
    public ICollection<HormoneProfile> HormoneProfiles { get; set; } = [];
    public ICollection<PeptideProfile> PeptideProfiles { get; set; } = [];
}
