namespace BioChain.Repository.Entities;

public class GateEntity
{
    public int Id { get; set; }
    public string GateType { get; set; } = "";       // AND, OR, NOT, XOR, NAND, NOR, THRESHOLD, GAIN, etc.
    public string? Symbol { get; set; }              // ⊼, ⊽, ¬, ⊕, ⊨, ⊳, ▷, ⊡, Σ, etc.
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
