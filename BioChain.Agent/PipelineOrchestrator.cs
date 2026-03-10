using BioChain.Parser;

namespace BioChain.Agent;

/// <summary>
/// The pipeline orchestrator. Receives user input, selects pipeline stage,
/// calls LLM with the correct prompt, parses the BNF output, and calls
/// Module reducers via the SpacetimeDB client SDK.
/// 
/// This is the only project that knows about LLMs.
/// </summary>
public class PipelineOrchestrator
{
    private readonly PromptStore _prompts;
    private readonly ILlmClient _llm;
    private readonly IModuleClient _module;

    public PipelineOrchestrator(PromptStore prompts, ILlmClient llm, IModuleClient module)
    {
        _prompts = prompts;
        _llm = llm;
        _module = module;
    }

    /// <summary>
    /// Full pipeline: user text → LLM → BNF → Parser → Reducer calls.
    /// </summary>
    public async Task<PipelineResult> RunAsync(string userInput, uint programId, PipelineStage stage)
    {
        // 1. Load the system prompt for this stage
        var systemPrompt = _prompts.Get(stage);

        // 2. If stage > Base, fetch existing program state to give LLM context
        var context = stage > PipelineStage.Base
            ? await _module.ReconstructBnfAsync(programId)
            : null;

        // 3. Call LLM — it generates BNF notation
        var bnfOutput = await _llm.GenerateAsync(systemPrompt, userInput, context);

        // 4. Parse BNF → structured commands
        var parseResult = BnfParser.Parse(bnfOutput);
        if (!parseResult.Success)
            return PipelineResult.ParseFailed(parseResult.Errors);

        // 5. Execute commands against Module reducers
        var execErrors = await _module.ExecuteCommandsAsync(programId, parseResult.Commands);

        // 6. Advance program stage if needed
        var stageNum = stage switch
        {
            PipelineStage.Base => (byte)1,
            PipelineStage.Plasticity => (byte)2,
            PipelineStage.Meta => (byte)3,
            PipelineStage.Convergence => (byte)4,
            _ => (byte)1,
        };
        await _module.SetProgramStageAsync(programId, stageNum);

        return execErrors.Count > 0
            ? PipelineResult.PartialSuccess(bnfOutput, execErrors)
            : PipelineResult.Ok(bnfOutput);
    }

    /// <summary>
    /// Determines which pipeline stage is appropriate given program state.
    /// </summary>
    public PipelineStage DetectStage(byte currentStage, string userIntent)
    {
        // Stage progression: Base → Plasticity → Meta → Convergence
        // Can also re-run a lower stage (e.g., new observation → Base again)
        // User intent keywords override automatic detection
        if (userIntent.Contains("convergence", StringComparison.OrdinalIgnoreCase)
            || userIntent.Contains("predict", StringComparison.OrdinalIgnoreCase))
            return PipelineStage.Convergence;

        if (userIntent.Contains("meta", StringComparison.OrdinalIgnoreCase)
            || userIntent.Contains("setpoint", StringComparison.OrdinalIgnoreCase)
            || userIntent.Contains("epigenetic", StringComparison.OrdinalIgnoreCase))
            return PipelineStage.Meta;

        if (userIntent.Contains("plasticity", StringComparison.OrdinalIgnoreCase)
            || userIntent.Contains("change", StringComparison.OrdinalIgnoreCase)
            || userIntent.Contains("delta", StringComparison.OrdinalIgnoreCase))
            return PipelineStage.Plasticity;

        // Default: advance one stage from current
        return currentStage switch
        {
            0 or 1 => PipelineStage.Base,
            2 => PipelineStage.Plasticity,
            3 => PipelineStage.Meta,
            4 => PipelineStage.Convergence,
            _ => PipelineStage.Base,
        };
    }
}

public record PipelineResult(bool Success, string? BnfOutput, List<string> Errors)
{
    public static PipelineResult Ok(string bnf) => new(true, bnf, []);
    public static PipelineResult ParseFailed(List<string> errors) => new(false, null, errors);
    public static PipelineResult PartialSuccess(string bnf, List<string> errors) => new(true, bnf, errors);
}
