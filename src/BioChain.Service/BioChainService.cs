using BioChain.Agent;
using BioChain.Models;
using SpacetimeDB.Types;

namespace BioChain.Service;

/// <summary>
/// Facade service — the single entry point for all BioChain operations.
/// Controllers depend only on this.
/// </summary>
public sealed class BioChainService
{
    private readonly PipelineOrchestrator _pipeline;
    private readonly ChatOrchestrator _chat;
    private readonly SpacetimeService _stdb;

    public BioChainService(PipelineOrchestrator pipeline, ChatOrchestrator chat, SpacetimeService stdb)
    {
        _pipeline = pipeline;
        _chat = chat;
        _stdb = stdb;
    }

    public Task<PipelineResult> GenerateAsync(PipelineRequest request, CancellationToken ct = default)
        => _pipeline.RunAsync(request, ct);

    public Task<PipelineResult> IngestAsync(string bnfText, string pipeline, string? programName = null, CancellationToken ct = default)
        => _pipeline.IngestRawAsync(bnfText, pipeline, programName, ct);

    public async Task SimulateAsync(ulong programId, List<Perturbation> perturbations)
        => await _stdb.SimulateAsync(programId, perturbations);

    public ProgramState GetProgram(ulong programId) => new()
    {
        ProgramId = programId,
        Nodes = _stdb.GetNodes(programId),
        Edges = _stdb.GetEdges(programId),
        Diagnostics = _stdb.GetDiags(programId)
    };

    public Task<ChatResult> ChatAsync(ulong programId, string message, List<ChatTurn>? history = null, CancellationToken ct = default)
        => _chat.ChatAsync(programId, message, history, ct);
}

public sealed class ProgramState
{
    public ulong ProgramId { get; init; }
    public List<Node> Nodes { get; init; } = [];
    public List<Edge> Edges { get; init; } = [];
    public List<Diag> Diagnostics { get; init; } = [];
}
