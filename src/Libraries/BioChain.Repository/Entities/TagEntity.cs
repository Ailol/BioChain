namespace BioChain.Repository.Entities;

public class TagEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string TagType { get; set; } = "";        // phenotype, trait, risk, domain, symptom, strength, custom
    public int? DomainId { get; set; }
    public string? Description { get; set; }
    public string? SeverityDefault { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
