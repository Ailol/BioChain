using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class Peptide
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Vector? Embedding { get; set; }

    // Navigation properties
    public ICollection<PeptideProfile> PeptideProfiles { get; set; } = [];
}
