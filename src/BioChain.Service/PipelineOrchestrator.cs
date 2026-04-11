using System.Text;
using BioChain.Agent;
using BioChain.Models;
using Microsoft.Extensions.Logging;

namespace BioChain.Service;

/// <summary>
/// Orchestrates the BNF pipeline: generate → ingest → validate.
/// Code-level coordinator — no LLM reasoning at this level.
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly SpacetimeService _stdb;
    private readonly LlmClient _llm;
    private readonly PromptStore _prompts;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        SpacetimeService stdb, LlmClient llm, PromptStore prompts, ILogger<PipelineOrchestrator> logger)
    {
        _stdb = stdb;
        _llm = llm;
        _prompts = prompts;
        _logger = logger;
    }

    /// <summary>
    /// Full pipeline: generate BNF from natural language, ingest, validate.
    /// For non-base pipelines, fetches existing layers from the program as context.
    /// </summary>
    public async Task<PipelineResult> RunAsync(PipelineRequest request, CancellationToken ct = default)
    {
        var result = new PipelineResult();
        var pipeline = request.Pipeline;

        // 1. Determine target program
        ulong programId;
        if (request.ProgramId.HasValue)
        {
            programId = request.ProgramId.Value;
            _logger.LogInformation("Using existing program {ProgramId} for pipeline={Pipeline}", programId, pipeline);
        }
        else
        {
            programId = await _stdb.CreateProgramAsync(
                request.ProgramName ?? $"llm-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                null, new List<string>());
            _logger.LogInformation("Created new program {ProgramId} for pipeline={Pipeline}", programId, pipeline);
        }
        result.ProgramId = programId;

        // 2. Build context from prior pipeline layers
        var userInput = request.Input;
        if (PromptStore.PipelineDependencies.TryGetValue(pipeline, out var deps) && deps.Length > 0)
        {
            var rawBnf = _stdb.GetProgramRawBnf(programId);
            var contextSb = new StringBuilder();

            foreach (var dep in deps)
            {
                if (rawBnf.TryGetValue(dep, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    contextSb.AppendLine($"=== {dep.ToUpperInvariant()} LAYER ===");
                    contextSb.AppendLine(raw);
                    contextSb.AppendLine();
                }
                else
                {
                    _logger.LogWarning("Program {ProgramId} missing {Layer} layer for {Pipeline} pipeline",
                        programId, dep, pipeline);
                }
            }

            if (contextSb.Length > 0)
            {
                userInput = $"{contextSb}\n=== TASK ===\n{request.Input}";
                _logger.LogInformation("Prepended {Deps} prior layer(s) as context ({Chars} chars)",
                    deps.Length, contextSb.Length);
            }
        }

        // 3. Generate BNF via LLM
        _logger.LogInformation("Generating BNF for pipeline={Pipeline}: {Input}",
            pipeline, request.Input[..Math.Min(100, request.Input.Length)]);

        var systemPrompt = _prompts.GetSystemPrompt(pipeline);
        var grammar = _prompts.GetGrammar(pipeline);
        var bnfText = await _llm.GenerateAsync(systemPrompt, userInput, grammar, ct);
        result.BnfText = bnfText;

        if (string.IsNullOrWhiteSpace(result.BnfText))
        {
            result.Error = "LLM returned empty BNF";
            return result;
        }

        _logger.LogInformation("Generated BNF ({Length} chars)", result.BnfText.Length);

        // 4. Ingest
        await _stdb.IngestBnfAsync(programId, pipeline, result.BnfText);

        // 5. Validate
        try
        {
            await _stdb.ValidateAsync(programId);
            result.ValidationPassed = true;
        }
        catch (Exception ex)
        {
            result.ValidationPassed = false;
            result.ValidationErrors = _stdb.GetDiagStrings(programId);
            _logger.LogWarning("Validation failed: {Error}", ex.Message);
        }

        // 6. Read counts
        result.NodeCount = _stdb.GetNodeCount(programId);
        result.EdgeCount = _stdb.GetEdgeCount(programId);

        return result;
    }

    /// <summary>
    /// Ingest raw BNF text directly (no LLM step).
    /// </summary>
    public async Task<PipelineResult> IngestRawAsync(
        string bnfText, string pipeline, string? programName = null, CancellationToken ct = default)
    {
        var result = new PipelineResult { BnfText = bnfText };

        var programId = await _stdb.CreateProgramAsync(
            programName ?? $"raw-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            null, new List<string>());
        result.ProgramId = programId;

        await _stdb.IngestBnfAsync(programId, pipeline, bnfText);

        try
        {
            await _stdb.ValidateAsync(programId);
            result.ValidationPassed = true;
        }
        catch
        {
            result.ValidationPassed = false;
            result.ValidationErrors = _stdb.GetDiagStrings(programId);
        }

        result.NodeCount = _stdb.GetNodeCount(programId);
        result.EdgeCount = _stdb.GetEdgeCount(programId);

        return result;
    }
}
