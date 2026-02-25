using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class ChatTools(NeuroService neuroService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "chat_respond")]
    [Description("Analyze a chat message someone sent TO you. Runs 27 biochemical agents, " +
                 "synthesizes reasoning, generates layer responses, and produces a suggested response. " +
                 "Example: chat_respond to karolina, relationship: Dating, text: I had a great time yesterday")]
    public async Task<string> ChatRespond(
        [Description("Person name who SENT the message")] string person,
        [Description("What they wrote to you")] string text,
        [Description("Relationship context (e.g., Dating, Friend, Colleague)")] string? relationship = null,
        [Description("Projected relationship direction (e.g., if currently Colleague but moving toward Dating)")] string? projected_relationship = null,
        [Description("Save analysis to personality profile (default: true). Set false for quick analysis without updating the profile.")] bool save = true)
    {
        var result = await neuroService.ChatRespondAsync(person, text, relationship, projected_relationship, save);
        return JsonSerializer.Serialize(new
        {
            person,
            source_type = "chat",
            decisions = result.Decisions.Select(d => new { d.Signal, d.Formula }),
            synthesis = result.Synthesis,
            layer_responses = result.LayerResponses,
            suggested_response = result.SuggestedResponse
        }, IndentedJson);
    }
}
