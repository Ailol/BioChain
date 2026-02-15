using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class NeuroTools(NeuroService neuroService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "neurorespond")]
    [Description("Analyze what someone wrote TO you. Runs a full neuroscan (27 biochemical agents), " +
                 "synthesizes reasoning, generates layer responses, and produces a suggested response. " +
                 "Example: neurorespond to karolina, relationship: Dating, text: I had a great time yesterday")]
    public async Task<string> Neurorespond(
        [Description("Person name who SENT the message")] string person,
        [Description("What they wrote to you")] string text,
        [Description("Relationship context (e.g., Dating, Friend, Colleague)")] string? relationship = null,
        [Description("Projected relationship direction (e.g., if currently Colleague but moving toward Dating)")] string? projected_relationship = null)
    {
        var result = await neuroService.NeuroRespondAsync(person, text, relationship, projected_relationship);
        return JsonSerializer.Serialize(new
        {
            person,
            decisions = result.Decisions.Select(d => new { d.Chemical, d.Reasoning }),
            synthesis = result.Synthesis,
            layer_responses = result.LayerResponses,
            suggested_response = result.SuggestedResponse
        }, IndentedJson);
    }
}
