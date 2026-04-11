namespace BioChain.Models;

public sealed class PipelineRequest
{
    public required string Input { get; init; }
    public string Pipeline { get; init; } = "base";
    public string? ProgramName { get; init; }
    public ulong? ProgramId { get; init; }
}

public sealed class PipelineResult
{
    public ulong ProgramId { get; set; }
    public string BnfText { get; set; } = "";
    public bool ValidationPassed { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public string? Error { get; set; }
}
