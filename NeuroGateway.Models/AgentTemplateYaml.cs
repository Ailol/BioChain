using YamlDotNet.Serialization;

namespace NeuroGateway.Models;

/// <summary>
/// YAML model for GroupAgents/*.yaml — analyzing agents (ADD/SKIP biochemical analysis).
/// </summary>
public class GroupAgentsYaml
{
    [YamlMember(Alias = "category")]
    public string Category { get; set; } = "";

    [YamlMember(Alias = "agents")]
    public List<AnalyzingAgentYaml> Agents { get; set; } = [];
}

public class AnalyzingAgentYaml
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "role")]
    public string Role { get; set; } = "";

    [YamlMember(Alias = "responsibilities")]
    public List<string> Responsibilities { get; set; } = [];

    [YamlMember(Alias = "style")]
    public string Style { get; set; } = "";

    [YamlMember(Alias = "max_words")]
    public int MaxWords { get; set; } = 100;

    [YamlMember(Alias = "sort_order")]
    public int SortOrder { get; set; }
}

/// <summary>
/// YAML model for LayerAgents/agents.yaml — unified neurochat config with relationship modes.
/// </summary>
public class UnifiedLayerAgentsYaml
{
    [YamlMember(Alias = "relationship_modes")]
    public Dictionary<string, RelationshipModeYaml> RelationshipModes { get; set; } = [];

    [YamlMember(Alias = "agents")]
    public List<NeuroChatAgentYaml> Agents { get; set; } = [];
}

public class RelationshipModeYaml
{
    [YamlMember(Alias = "label")]
    public string Label { get; set; } = "";

    [YamlMember(Alias = "tone")]
    public string Tone { get; set; } = "";

    [YamlMember(Alias = "goal")]
    public string Goal { get; set; } = "";

    [YamlMember(Alias = "synth_instruction")]
    public string SynthInstruction { get; set; } = "";

    [YamlMember(Alias = "layer_max_words")]
    public int LayerMaxWords { get; set; } = 80;

    [YamlMember(Alias = "synth_max_words")]
    public int SynthMaxWords { get; set; } = 100;
}

public class NeuroChatAgentYaml
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    [YamlMember(Alias = "layer")]
    public string? Layer { get; set; }

    [YamlMember(Alias = "role")]
    public string Role { get; set; } = "";

    [YamlMember(Alias = "style")]
    public string Style { get; set; } = "";

    [YamlMember(Alias = "max_words")]
    public int MaxWords { get; set; } = 80;

    [YamlMember(Alias = "sort_order")]
    public int SortOrder { get; set; }

    [YamlMember(Alias = "is_synthesizer")]
    public bool IsSynthesizer { get; set; }
}
