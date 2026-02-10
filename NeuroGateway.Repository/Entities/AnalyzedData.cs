using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class AnalyzedData
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Content { get; set; } = "";
    public string? SourceType { get; set; }
    public string? SourceUri { get; set; }
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Person Person { get; set; } = null!;
    public ICollection<NeurotransmitterProfile> NeurotransmitterProfiles { get; set; } = [];
    public ICollection<HormoneProfile> HormoneProfiles { get; set; } = [];
    public ICollection<PeptideProfile> PeptideProfiles { get; set; } = [];
}
