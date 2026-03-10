namespace BioChain.Agent;

/// <summary>
/// Which pipeline stage the agent selects based on available data.
/// Maps 1:1 to the four system prompts.
/// </summary>
public enum PipelineStage
{
    Base,           // First observation → biochain-base.md
    Plasticity,     // Two+ snapshots → biochain-plasticity.md
    Meta,           // Sustained trends → biochain-meta.md
    Convergence,    // Full diamond → biochain-convergence.md
}

/// <summary>
/// Loads system prompts from disk. Prompts are the pipeline-specific
/// instructions that tell the LLM how to generate BNF output.
/// </summary>
public class PromptStore
{
    private readonly Dictionary<PipelineStage, string> _prompts = [];
    private readonly string _promptDir;

    public PromptStore(string promptDir)
    {
        _promptDir = promptDir;
    }

    public string Get(PipelineStage stage)
    {
        if (_prompts.TryGetValue(stage, out var cached))
            return cached;

        var filename = stage switch
        {
            PipelineStage.Base => "biochain-base.md",
            PipelineStage.Plasticity => "biochain-plasticity.md",
            PipelineStage.Meta => "biochain-meta.md",
            PipelineStage.Convergence => "biochain-convergence.md",
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

        var path = Path.Combine(_promptDir, filename);
        var text = File.ReadAllText(path);
        _prompts[stage] = text;
        return text;
    }
}
