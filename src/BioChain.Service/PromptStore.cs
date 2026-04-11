using BioChain.Agent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioChain.Service;

/// <summary>
/// Loads and caches system prompts and grammars for each pipeline layer.
/// Owns the pipeline dependency DAG.
/// </summary>
public sealed class PromptStore
{
    private readonly LlmOptions _opts;
    private readonly ILogger<PromptStore> _logger;
    private readonly Dictionary<string, string> _systemPrompts = new();
    private readonly Dictionary<string, string> _grammars = new();

    private static readonly Dictionary<string, string> PipelineToPromptFile = new()
    {
        ["base"] = "BASE_SYSTEM_PROMPT.md",
        ["plasticity"] = "PLASTICITY_SYSTEM_PROMPT.md",
        ["meta"] = "META_SYSTEM_PROMPT.md",
        ["convergence"] = "CONVERGENCE_SYSTEM_PROMPT.md",
        ["chat"] = "CHAT_SYSTEM_PROMPT.txt",
    };

    private static readonly Dictionary<string, string> PipelineToGrammarFile = new()
    {
        ["base"] = "biochain_base.ebnf",
        ["plasticity"] = "biochain_plasticity.ebnf",
        ["meta"] = "biochain_meta.ebnf",
        ["convergence"] = "biochain_convergence.ebnf",
    };

    /// <summary>
    /// Required prior pipeline layers for each pipeline type.
    /// plasticity needs base, meta needs base+plasticity, convergence needs all three.
    /// </summary>
    public static readonly Dictionary<string, string[]> PipelineDependencies = new()
    {
        ["base"] = [],
        ["plasticity"] = ["base"],
        ["meta"] = ["base", "plasticity"],
        ["convergence"] = ["base", "plasticity", "meta"],
    };

    public PromptStore(IOptions<LlmOptions> opts, ILogger<PromptStore> logger)
    {
        _opts = opts.Value;
        _logger = logger;
        LoadPrompts();
        LoadGrammars();
    }

    public string GetSystemPrompt(string pipeline)
    {
        if (_systemPrompts.TryGetValue(pipeline, out var prompt))
            return prompt;
        return GetFallbackPrompt(pipeline);
    }

    /// <summary>
    /// Get the chat system prompt with {context} replaced by the program's network context.
    /// </summary>
    public string GetChatPrompt(string networkContext)
    {
        var template = GetSystemPrompt("chat");
        return template.Replace("{context}", networkContext);
    }

    public string? GetGrammar(string pipeline)
    {
        if (!_opts.UseGrammar) return null;
        return _grammars.GetValueOrDefault(pipeline);
    }

    private void LoadPrompts()
    {
        foreach (var (pipeline, file) in PipelineToPromptFile)
        {
            var path = Path.Combine(_opts.SystemPromptsDir, file);
            if (File.Exists(path))
            {
                _systemPrompts[pipeline] = File.ReadAllText(path);
                _logger.LogInformation("Loaded system prompt for {Pipeline} ({Chars} chars)",
                    pipeline, _systemPrompts[pipeline].Length);
            }
            else
            {
                _logger.LogWarning("System prompt file not found: {Path}, using fallback", path);
                _systemPrompts[pipeline] = GetFallbackPrompt(pipeline);
            }
        }
    }

    private void LoadGrammars()
    {
        if (!_opts.UseGrammar) return;

        foreach (var (pipeline, file) in PipelineToGrammarFile)
        {
            var path = Path.Combine(_opts.GrammarsDir, file);
            if (File.Exists(path))
            {
                _grammars[pipeline] = File.ReadAllText(path);
                _logger.LogInformation("Loaded grammar for {Pipeline} ({Chars} chars)",
                    pipeline, _grammars[pipeline].Length);
            }
            else
            {
                _logger.LogWarning("Grammar file not found: {Path}, constrained decoding disabled for {Pipeline}",
                    path, pipeline);
            }
        }
    }

    private static string GetFallbackPrompt(string pipeline) => pipeline switch
    {
        "base" => "You are a biochemical notation expert. Output valid BioChain BASE pipeline BNF notation. NO prose.",
        "plasticity" => "You are a biochemical notation expert. Output valid BioChain PLASTICITY pipeline BNF notation. NO prose.",
        "meta" => "You are a biochemical notation expert. Output valid BioChain META pipeline BNF notation. NO prose.",
        "convergence" => "You are a biochemical notation expert. Output valid BioChain CONVERGENCE pipeline BNF notation. NO prose.",
        "chat" => "You are a biochemical systems neuroscience expert. Answer questions about the biochemical network.\n\n{context}",
        _ => "You are a biochemical notation expert. Output valid BioChain BNF notation."
    };
}
