using Pgvector;

namespace Repository.Entities;

public class HormoneProfile
{
    public int Id { get; set; }
    public int PersonalityId { get; set; }
    public int HormoneId { get; set; }
    public string? Reasoning { get; set; }
    public Vector? ReasoningEmbedding { get; set; }
    public int? ClusterId { get; set; }
    public bool IsClusterRepresentative { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Personality Personality { get; set; } = null!;
    public Hormone Hormone { get; set; } = null!;
}
