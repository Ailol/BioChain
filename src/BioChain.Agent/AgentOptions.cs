namespace BioChain.Agent;

public sealed class LlmOptions
{
    public const string Section = "Llm";
    public string Endpoint { get; set; } = "http://localhost:8000";
    public string Model { get; set; } = "/models/Qwen3.5-A3B";
    /// <summary>Path to directory containing system prompt .md files.</summary>
    public string SystemPromptsDir { get; set; } = "system-prompts";
    /// <summary>Path to directory containing EBNF grammar files.</summary>
    public string GrammarsDir { get; set; } = "xgrammar";
    /// <summary>Enable guided_grammar constrained decoding.</summary>
    public bool UseGrammar { get; set; } = true;
}

public sealed class SpacetimeOptions
{
    public const string Section = "SpacetimeDb";
    /// <summary>
    /// SpacetimeDB host URL. The SDK connects via WebSocket (ws:// or wss://).
    /// Accepts http:// or ws:// — the SDK handles protocol upgrade.
    /// </summary>
    public string Host { get; set; } = "http://localhost:3000";
    public string Database { get; set; } = "biochain";
}
