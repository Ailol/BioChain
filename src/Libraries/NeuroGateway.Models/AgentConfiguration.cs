namespace NeuroGateway.Models;

/// <summary>
/// Per-purpose LLM provider configuration.
/// Auto-detects backend from Endpoint/ApiKey:
///   - ApiKey starts with "sk-ant" → Anthropic
///   - No Endpoint → OpenAI (official endpoint)
///   - Endpoint port 11434 → Ollama (native client)
///   - Everything else → OpenAI-compatible (vLLM, RunPod, etc.)
/// </summary>
public class LlmProviderConfig
{
    public string? Backend { get; set; }
    public string? Endpoint { get; set; }
    public string Model { get; set; } = "";
    public string? ApiKey { get; set; }

    public string ResolvedBackend
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Backend))
                return Backend switch
                {
                    var b when b.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) => "Anthropic",
                    var b when b.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) => "OpenAI",
                    var b when b.Equals("Ollama", StringComparison.OrdinalIgnoreCase) => "Ollama",
                    _ => "OpenAiCompatible"
                };

            if (ApiKey?.StartsWith("sk-ant", StringComparison.OrdinalIgnoreCase) == true)
                return "Anthropic";
            if (string.IsNullOrWhiteSpace(Endpoint))
                return "OpenAI";
            if (Endpoint.Contains(":11434"))
                return "Ollama";

            return "OpenAiCompatible";
        }
    }
}

/// <summary>
/// Root LLM configuration with four provider slots:
///   Orchestrator   — VL model for MCP tool orchestrating, scan synthesis
///   AgentAnalyzing — LoRA model for 27 biochem analyzing agents (SKIP/ADD)
///   AgentLayer     — layer model for neurochat layer agents + synthesizer
///   Embedding      — vector generation
/// </summary>
public class AgentConfiguration
{
    public LlmProviderConfig? Orchestrator { get; set; }
    public LlmProviderConfig? AgentAnalyzing { get; set; }
    public LlmProviderConfig? AgentLayer { get; set; }
    public LlmProviderConfig? Embedding { get; set; }
    public int MaxParallelAgents { get; set; } = 3;

    public void Validate()
    {
        if (AgentAnalyzing is null)
            throw new InvalidOperationException("Llm:AgentAnalyzing section is required");

        ValidateProvider(AgentAnalyzing, "AgentAnalyzing");

        if (Orchestrator is not null)
            ValidateProvider(Orchestrator, "Orchestrator");
        if (AgentLayer is not null)
            ValidateProvider(AgentLayer, "AgentLayer");
        if (Embedding is not null)
            ValidateProvider(Embedding, "Embedding");
    }

    private static void ValidateProvider(LlmProviderConfig cfg, string purpose)
    {
        if (string.IsNullOrWhiteSpace(cfg.Model))
            throw new InvalidOperationException($"Llm:{purpose}:Model is required");

        var backend = cfg.ResolvedBackend;
        if (backend is "Ollama" or "OpenAiCompatible" && string.IsNullOrWhiteSpace(cfg.Endpoint))
            throw new InvalidOperationException($"Llm:{purpose}:Endpoint is required (detected backend: {backend})");
        if (backend is "Anthropic" or "OpenAI" && string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException($"Llm:{purpose}:ApiKey is required (detected backend: {backend})");
    }
}
