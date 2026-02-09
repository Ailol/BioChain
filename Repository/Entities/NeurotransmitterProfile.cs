namespace Repository.Entities;

public class NeurotransmitterProfile
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int NeurotransmitterId { get; set; }
    public string? Reasoning { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Personality Personality { get; set; } = null!;
    public Neurotransmitter Neurotransmitter { get; set; } = null!;
}
