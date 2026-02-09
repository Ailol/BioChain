namespace Models;

/// <summary>
/// Configuration for a responder group loaded from JSON.
/// </summary>
public class ResponderGroupConfig
{
    public string Context { get; set; } = "";
    public List<ResponderGroupAgent> Agents { get; set; } = [];
}

/// <summary>
/// Agent definition within a responder group.
/// </summary>
public class ResponderGroupAgent
{
    public string Name { get; set; } = "";
    public string? Neurotransmitter { get; set; }
    public string? Layer { get; set; }  // "neurotransmitter", "hormone", "peptide", or null (synthesizer)
    public string Role { get; set; } = "";
    public string Style { get; set; } = "";
    public int MaxWords { get; set; } = 150;
    public bool IsSynthesizer { get; set; } = false;
}
