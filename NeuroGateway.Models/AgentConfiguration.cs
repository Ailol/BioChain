namespace NeuroGateway.Models;

/// <summary>
/// Per-purpose LLM provider configuration.
/// Only Endpoint + Model required. Backend is auto-detected:
///   - ApiKey starts with "sk-ant" → Anthropic
///   - No Endpoint → OpenAI (official endpoint)
///   - Endpoint port 11434 → Ollama (native client)
///   - Everything else → OpenAI-compatible (vLLM, RunPod, etc.)
/// Set Backend explicitly only to override auto-detection.
/// </summary>
public class LlmProviderConfig
{
    /// <summary>Optional backend override. Auto-detected from Endpoint/ApiKey if omitted.</summary>
    public string? Backend { get; set; }

    /// <summary>Endpoint URL. Required for all backends except OpenAI/Anthropic (which use official endpoints).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Model name/ID as known to the provider.</summary>
    public string Model { get; set; } = "";

    /// <summary>API key. Required for Anthropic/OpenAI. Optional for OpenAI-compatible endpoints.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Resolved backend type based on explicit Backend, or auto-detected from Endpoint/ApiKey.
    /// Returns: "Anthropic", "OpenAI", "Ollama", or "OpenAiCompatible".
    /// </summary>
    public string ResolvedBackend
    {
        get
        {
            // Explicit override takes priority
            if (!string.IsNullOrWhiteSpace(Backend))
                return Backend switch
                {
                    var b when b.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) => "Anthropic",
                    var b when b.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) => "OpenAI",
                    var b when b.Equals("Ollama", StringComparison.OrdinalIgnoreCase) => "Ollama",
                    _ => "OpenAiCompatible" // Vllm, RunPod, or any other → same path
                };

            // Auto-detect from ApiKey + Endpoint
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
/// Root LLM configuration with three independent provider slots:
///   Orchestrator    — VL model for MCP tool orchestrating, trait extraction, style gen, scan synthesis
///   AgentFramework  — instruct model for biochem agents, neuroresponse layers, group chat
///   Embedding       — vector generation
///
/// Each slot needs only Endpoint + Model. ApiKey for cloud providers. Backend is auto-detected.
/// Configure in appsettings.json (non-secret) and .env (API keys).
/// </summary>
public class AgentConfiguration
{
    // ── Per-purpose provider configs ──
    public LlmProviderConfig? Orchestrator { get; set; }
    public LlmProviderConfig? AgentFramework { get; set; }
    public LlmProviderConfig? Embedding { get; set; }

    // ── Shared settings ──
    public required string PersonalityDb { get; set; }
    public int MaxParallelAgents { get; set; } = 3;

    /// <summary>Validate that each provider has the required fields.</summary>
    public void Validate()
    {
        if (Orchestrator is null)
            throw new InvalidOperationException("Llm:Orchestrator section is required");
        if (AgentFramework is null)
            throw new InvalidOperationException("Llm:AgentFramework section is required");
        if (Embedding is null)
            throw new InvalidOperationException("Llm:Embedding section is required");

        ValidateProvider(Orchestrator, "Orchestrator");
        ValidateProvider(AgentFramework, "AgentFramework");
        ValidateProvider(Embedding, "Embedding");

        if (Embedding.ResolvedBackend == "Anthropic")
            throw new InvalidOperationException("Anthropic does not support embeddings — choose a different provider for Llm:Embedding");
    }

    private static void ValidateProvider(LlmProviderConfig cfg, string purpose)
    {
        if (string.IsNullOrWhiteSpace(cfg.Model))
            throw new InvalidOperationException($"Llm:{purpose}:Model is required");

        var backend = cfg.ResolvedBackend;

        // Endpoint required for Ollama and OpenAI-compatible
        if (backend is "Ollama" or "OpenAiCompatible" && string.IsNullOrWhiteSpace(cfg.Endpoint))
            throw new InvalidOperationException($"Llm:{purpose}:Endpoint is required (detected backend: {backend})");

        // ApiKey required for Anthropic and OpenAI (no custom endpoint)
        if (backend is "Anthropic" or "OpenAI" && string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException($"Llm:{purpose}:ApiKey is required (detected backend: {backend})");
    }
}
