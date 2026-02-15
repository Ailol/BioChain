using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class WorkTools(NeuroService neuroService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "work_analyze")]
    [Description("Analyze professional content (CVs, emails, reports, meeting notes). " +
                 "Runs full 27-agent biochemical scan then synthesizes findings. " +
                 "Reveals decision-making style, stress response, leadership patterns, conflict avoidance. " +
                 "What gaps and absences reveal is as important as what's present.")]
    public async Task<string> WorkAnalyze(
        [Description("Person name being analyzed")] string person,
        [Description("Content to analyze (CV text, email body, report, meeting notes)")] string text,
        [Description("Professional relationship context (e.g., Colleague, Manager, Client)")] string? relationship = null,
        [Description("Save analysis to personality profile (default: true). Set false for quick analysis without updating the profile.")] bool save = true)
    {
        var result = await neuroService.WorkAnalyzeAsync(person, text, relationship, save);
        return JsonSerializer.Serialize(new
        {
            person,
            source_type = "work",
            decisions_count = result.Decisions.Count,
            decisions = result.Decisions.Select(d => new { d.Chemical, d.Reasoning }),
            synthesis = result.Synthesis
        }, IndentedJson);
    }
}
