using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class AnalyzeTools(AnalyzeService analyzeService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "analyze_data")]
    [Description("Analyze text/conversation content to extract biochemical personality traits. " +
                 "Runs 27 biochemical agents (neurotransmitters, hormones, peptides) to evaluate the content.")]
    public async Task<string> AnalyzeData(
        [Description("Raw content to analyze")] string fileContent,
        [Description("Name of person being analyzed")] string targetPersonalityName,
        [Description("Relationship context (e.g., Dating, Friend)")] string? relationship = null)
    {
        var decisions = await analyzeService.AnalyzeAsync(
            targetPersonalityName, fileContent, relationship, sourceType: "file");
        return JsonSerializer.Serialize(new
        {
            person = targetPersonalityName,
            decisionsCount = decisions.Count,
            decisions = decisions.Select(d => new { d.Chemical, d.Reasoning })
        }, IndentedJson);
    }
}
