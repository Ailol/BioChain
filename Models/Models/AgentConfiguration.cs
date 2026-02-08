namespace Models;

/// <summary>
/// Configuration settings for all agent services.
/// Supports Ollama and vLLM (OpenAI-compatible) backends.
/// </summary>
public class AgentConfiguration
{
    public string Backend { get; set; } = "Ollama";
    public required string ChatEndpoint { get; set; }
    public string? EmbeddingEndpoint { get; set; }
    public required string ThinkingModel { get; set; }
    public required string InstructModel { get; set; }
    public required string EmbeddingModel { get; set; }
    public required string PersonalityDb { get; set; }
    public int MaxParallelAgents { get; set; } = 3;
}
