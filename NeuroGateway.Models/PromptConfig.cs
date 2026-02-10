using System.Text.Json;

namespace NeuroGateway.Models;

/// <summary>
/// Typed config loaded from Prompts.json.
/// </summary>
public class PromptConfig
{
    public AgentGenerationConfig AgentGeneration { get; set; } = new();
    public SuggestionConfig Suggestions { get; set; } = new();
    public ConversationAnalysisConfig ConversationAnalysis { get; set; } = new();

    public string AgentJsonExample => JsonSerializer.Serialize(AgentGeneration.JsonExample, new JsonSerializerOptions { WriteIndented = true });
    public Dictionary<string, List<string>> RoleExamples => AgentGeneration.RoleExamples;
}

public class AgentGenerationConfig
{
    public List<JsonElement> JsonExample { get; set; } = [];
    public Dictionary<string, List<string>> RoleExamples { get; set; } = new();
}

public class SuggestionConfig
{
    public string TopicTemplate { get; set; } = "";
}

public class ConversationAnalysisConfig
{
    public List<JsonElement> JsonExample { get; set; } = [];
    public string PromptTemplate { get; set; } = "";
}
