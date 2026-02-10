using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class Hormone
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Vector? Embedding { get; set; }

    // Navigation properties
    public ICollection<HormoneProfile> HormoneProfiles { get; set; } = [];
}
