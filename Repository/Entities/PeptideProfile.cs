namespace Repository.Entities;

public class PeptideProfile
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int PeptideId { get; set; }
    public string? Reasoning { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Personality Personality { get; set; } = null!;
    public Peptide Peptide { get; set; } = null!;
}
