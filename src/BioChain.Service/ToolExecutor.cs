using System.Text;
using System.Text.Json;
using BioChain.Agent;
using BioChain.Models;
using Microsoft.Extensions.Logging;
using SpacetimeDB.Types;

namespace BioChain.Service;

/// <summary>
/// Dispatches and executes tool calls against SpacetimeDB and the LLM pipeline.
/// </summary>
public sealed class ToolExecutor
{
    private readonly SpacetimeService _stdb;
    private readonly LlmClient _llm;
    private readonly PromptStore _prompts;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(SpacetimeService stdb, LlmClient llm, PromptStore prompts, ILogger<ToolExecutor> logger)
    {
        _stdb = stdb;
        _llm = llm;
        _prompts = prompts;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(ulong programId, string toolName, string argumentsJson, CancellationToken ct)
    {
        using var args = JsonDocument.Parse(argumentsJson);
        var root = args.RootElement;

        return toolName switch
        {
            "simulate" => await ExecuteSimulateAsync(programId, root),
            "get_program_state" => ExecuteGetProgramState(programId),
            "search_nodes" => ExecuteSearchNodes(programId, root),
            "get_simulation_results" => ExecuteGetSimResults(programId),
            "predict_plasticity" => await ExecutePipelineAsync(programId, "plasticity", ct),
            "infer_meta_programs" => await ExecutePipelineAsync(programId, "meta", ct),
            "compute_convergence" => await ExecutePipelineAsync(programId, "convergence", ct),
            _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
        };
    }

    private async Task<string> ExecuteSimulateAsync(ulong programId, JsonElement args)
    {
        var perturbations = new List<Perturbation>();
        if (args.TryGetProperty("perturbations", out var pertsEl))
        {
            foreach (var p in pertsEl.EnumerateArray())
            {
                perturbations.Add(new Perturbation
                {
                    TargetCode = p.GetProperty("target_code").GetString() ?? "",
                    TargetRegion = p.GetProperty("target_region").GetString() ?? "",
                    Action = p.GetProperty("action").GetString() ?? "",
                    Value = p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number
                        ? v.GetSingle() : null
                });
            }
        }

        var maxTicks = args.TryGetProperty("max_ticks", out var mt) && mt.ValueKind == JsonValueKind.Number
            ? (uint)mt.GetInt32() : 1000u;

        await _stdb.SimulateAsync(programId, perturbations, maxTicks);

        return JsonSerializer.Serialize(new
        {
            status = "simulation_complete",
            program_id = programId,
            perturbations_applied = perturbations.Select(p => new
            {
                target = $"{p.TargetCode}@{p.TargetRegion}",
                action = p.Action,
                value = p.Value
            }),
            network_size = new { nodes = _stdb.GetNodeCount(programId), edges = _stdb.GetEdgeCount(programId) },
            note = "Use get_program_state to see the updated network state after simulation."
        });
    }

    private string ExecuteGetProgramState(ulong programId)
        => _stdb.GetProgramContext(programId);

    private string ExecuteSearchNodes(ulong programId, JsonElement args)
    {
        var pattern = args.GetProperty("code_pattern").GetString() ?? "";
        var results = _stdb.SearchNodes(programId, pattern);
        return JsonSerializer.Serialize(new { pattern, matches = results });
    }

    private string ExecuteGetSimResults(ulong programId)
    {
        var simRuns = _stdb.GetSimRuns(programId);
        if (simRuns.Count > 0)
        {
            return JsonSerializer.Serialize(new
            {
                program_id = programId,
                simulation_count = simRuns.Count,
                runs = simRuns.Select(r => new
                {
                    id = r.Id,
                    max_ticks = r.MaxTicks,
                    final_tick = r.FinalTick,
                    status = r.Status,
                    perturbations = r.Perturbations.Select(p => new
                    {
                        target_code = p.TargetCode,
                        target_region = p.TargetRegion,
                        action = p.Action,
                        value = p.Value
                    })
                })
            });
        }
        return JsonSerializer.Serialize(new { program_id = programId, simulation_count = 0 });
    }

    /// <summary>
    /// Execute an analysis pipeline (plasticity/meta/convergence) as a tool.
    /// Auto-runs prerequisite layers if missing.
    /// </summary>
    private async Task<string> ExecutePipelineAsync(ulong programId, string pipeline, CancellationToken ct)
    {
        _logger.LogInformation("Pipeline tool: running {Pipeline} on program {ProgramId}", pipeline, programId);

        var rawBnf = _stdb.GetProgramRawBnf(programId);

        // Auto-run prerequisite layers if missing
        if (PromptStore.PipelineDependencies.TryGetValue(pipeline, out var deps))
        {
            foreach (var dep in deps)
            {
                if (string.IsNullOrWhiteSpace(rawBnf.GetValueOrDefault(dep)))
                {
                    _logger.LogInformation("Auto-running prerequisite pipeline: {Dep}", dep);
                    await ExecutePipelineAsync(programId, dep, ct);
                    rawBnf = _stdb.GetProgramRawBnf(programId);
                }
            }
        }

        // Build context from prior layers
        var contextSb = new StringBuilder();
        foreach (var dep in deps ?? [])
        {
            var raw = rawBnf.GetValueOrDefault(dep);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                contextSb.AppendLine($"=== {dep.ToUpperInvariant()} LAYER ===");
                contextSb.AppendLine(raw);
                contextSb.AppendLine();
            }
        }

        var systemPrompt = _prompts.GetSystemPrompt(pipeline);
        var grammar = _prompts.GetGrammar(pipeline);
        var userInput = contextSb.Length > 0
            ? $"{contextSb}\n=== TASK ===\nProject the {pipeline} layer from the above state."
            : $"Project the {pipeline} layer.";

        var bnfText = await _llm.GenerateAsync(systemPrompt, userInput, grammar, ct);

        if (string.IsNullOrWhiteSpace(bnfText))
            return JsonSerializer.Serialize(new { error = $"{pipeline} pipeline returned empty output" });

        await _stdb.IngestBnfAsync(programId, pipeline, bnfText);
        _logger.LogInformation("Pipeline tool {Pipeline}: generated {Len} chars, ingested into program {ProgramId}",
            pipeline, bnfText.Length, programId);

        return JsonSerializer.Serialize(new
        {
            pipeline,
            status = "complete",
            program_id = programId,
            bnf_length = bnfText.Length,
            output = bnfText
        });
    }
}
