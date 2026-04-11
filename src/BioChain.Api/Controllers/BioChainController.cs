using BioChain.Models;
using BioChain.Service;
using Microsoft.AspNetCore.Mvc;
using SpacetimeDB.Types;

namespace BioChain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BioChainController : ControllerBase
{
    private readonly BioChainService _svc;

    public BioChainController(BioChainService svc) => _svc = svc;

    /// <summary>
    /// Full pipeline: natural language → LLM → BNF → parse → validate.
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<PipelineResult>> Generate(
        [FromBody] GenerateRequest request, CancellationToken ct)
    {
        var result = await _svc.GenerateAsync(new PipelineRequest
        {
            Input = request.Input,
            Pipeline = request.Pipeline ?? "base",
            ProgramName = request.ProgramName,
            ProgramId = request.ProgramId
        }, ct);

        return Ok(result);
    }

    /// <summary>
    /// Ingest raw BNF text directly (skip LLM).
    /// </summary>
    [HttpPost("ingest")]
    public async Task<ActionResult<PipelineResult>> Ingest(
        [FromBody] IngestRequest request, CancellationToken ct)
    {
        var result = await _svc.IngestAsync(
            request.BnfText, request.Pipeline ?? "base", request.ProgramName, ct);
        return Ok(result);
    }

    /// <summary>
    /// Run simulation on an existing program.
    /// </summary>
    [HttpPost("simulate/{programId}")]
    public async Task<ActionResult> Simulate(ulong programId, [FromBody] SimulateRequest request)
    {
        var perturbations = request.Perturbations.Select(p =>
            new Perturbation
            {
                TargetCode = p.TargetCode,
                TargetRegion = p.TargetRegion,
                Action = p.Action,
                Value = p.Value
            }).ToList();

        await _svc.SimulateAsync(programId, perturbations);
        return Ok(new { ProgramId = programId, Status = "simulated" });
    }

    /// <summary>
    /// Get program state (nodes, edges, diagnostics).
    /// </summary>
    [HttpGet("program/{programId}")]
    public ActionResult GetProgram(ulong programId)
        => Ok(_svc.GetProgram(programId));

    /// <summary>
    /// Chat about a program's biochemical network.
    /// </summary>
    [HttpPost("chat/{programId}")]
    public async Task<ActionResult<ChatResult>> Chat(
        ulong programId, [FromBody] ChatRequest request, CancellationToken ct)
    {
        var result = await _svc.ChatAsync(programId, request.Message, request.History, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    public ActionResult Health() => Ok(new { Status = "ok" });
}

public record GenerateRequest(string Input, string? Pipeline = "base", string? ProgramName = null, ulong? ProgramId = null);
public record IngestRequest(string BnfText, string? Pipeline = "base", string? ProgramName = null);
public record SimulateRequest(List<PerturbationDto> Perturbations);
public record PerturbationDto(string TargetCode, string TargetRegion, string Action, float? Value);
public record ChatRequest(string Message, List<ChatTurn>? History = null);
