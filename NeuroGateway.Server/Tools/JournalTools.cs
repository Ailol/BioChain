using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class JournalTools(NeuroService neuroService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "journal_analyze")]
    [Description("Analyze self-reflective content (diary entries, personal notes, voice transcripts). " +
                 "Runs full 27-agent biochemical scan then synthesizes findings. " +
                 "Captures metacognition signals — how the person observes themselves. " +
                 "Best for temporal drift tracking across multiple entries.")]
    public async Task<string> JournalAnalyze(
        [Description("Person name (the person writing the journal/reflection)")] string person,
        [Description("Journal entry, diary text, personal reflection, or voice transcript")] string text,
        [Description("Save analysis to personality profile (default: true). Set false for quick analysis without updating the profile.")] bool save = true)
    {
        var result = await neuroService.JournalAnalyzeAsync(person, text, save);
        return JsonSerializer.Serialize(new
        {
            person,
            source_type = "journal",
            decisions_count = result.Decisions.Count,
            decisions = result.Decisions.Select(d => new { d.Chemical, d.Reasoning }),
            synthesis = result.Synthesis
        }, IndentedJson);
    }
}
