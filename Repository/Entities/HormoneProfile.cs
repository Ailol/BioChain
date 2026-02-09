namespace Repository.Entities;

public class HormoneProfile
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int HormoneId { get; set; }
    public string? Reasoning { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Personality Personality { get; set; } = null!;
    public Hormone Hormone { get; set; } = null!;
}
