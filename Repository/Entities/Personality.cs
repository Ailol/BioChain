using Pgvector;

namespace Repository.Entities;

public class Personality
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Topic { get; set; } = "";
    public string? Explanation { get; set; }
    public string? ExplanatoryContext { get; set; }
    public Vector? Embedding { get; set; }
    public string? SourceType { get; set; }
    public string? SourceUri { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Person Person { get; set; } = null!;
    public ICollection<NeurotransmitterProfile> NeurotransmitterProfiles { get; set; } = [];
    public ICollection<HormoneProfile> HormoneProfiles { get; set; } = [];
    public ICollection<PeptideProfile> PeptideProfiles { get; set; } = [];
}
