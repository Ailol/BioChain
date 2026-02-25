using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NeuroGateway.Service;
using NeuroGateway.Utils.Parsing;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class AnalyzeTools(NeuroService neuroService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "work_analyze")]
    [Description("Analyze professional content (CVs, emails, reports, meeting notes). " +
                 "Runs full 27-agent biochemical scan then synthesizes findings. " +
                 "Reveals decision-making style, stress response, leadership patterns, conflict avoidance. " +
                 "What gaps and absences reveal is as important as what's present.")]
    public async Task<string> WorkAnalyze(
        [Description("Person name being analyzed")] string person,
        [Description("Content to analyze (CV text, email body, report, meeting notes). " +
                     "Provide plain text OR base64-encoded file content (set document_type to 'pdf' or 'docx').")] string text,
        [Description("Professional relationship context (e.g., Colleague, Manager, Client)")] string? relationship = null,
        [Description("Document type when text is base64-encoded file content: 'pdf' or 'docx'. " +
                     "Leave empty for plain text.")] string? document_type = null,
        [Description("Save analysis to personality profile (default: true). " +
                     "Set false for quick analysis without updating the profile.")] bool save = true,
        IProgress<ProgressNotificationValue>? progress = null)
    {
        var extractedText = ExtractIfNeeded(text, document_type, progress);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 1, Total = 3,
            Message = "Running 27 biochemical agents..."
        });

        var result = await neuroService.WorkAnalyzeAsync(person, extractedText, relationship, save);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3, Total = 3,
            Message = $"Complete — {result.Decisions.Count} chemicals detected"
        });

        return JsonSerializer.Serialize(new
        {
            person,
            source_type = "work",
            decisions_count = result.Decisions.Count,
            decisions = result.Decisions.Select(d => new { d.Signal, d.Formula }),
            synthesis = result.Synthesis
        }, IndentedJson);
    }

    [McpServerTool(Name = "journal_analyze")]
    [Description("Analyze self-reflective content (diary entries, personal notes, voice transcripts). " +
                 "Runs full 27-agent biochemical scan then synthesizes findings. " +
                 "Captures metacognition signals — how the person observes themselves. " +
                 "Best for temporal drift tracking across multiple entries.")]
    public async Task<string> JournalAnalyze(
        [Description("Person name (the person writing the journal/reflection)")] string person,
        [Description("Journal entry, diary text, personal reflection, or voice transcript. " +
                     "Provide plain text OR base64-encoded file content (set document_type to 'pdf' or 'docx').")] string text,
        [Description("Document type when text is base64-encoded file content: 'pdf' or 'docx'. " +
                     "Leave empty for plain text.")] string? document_type = null,
        [Description("Save analysis to personality profile (default: true). " +
                     "Set false for quick analysis without updating the profile.")] bool save = true,
        IProgress<ProgressNotificationValue>? progress = null)
    {
        var extractedText = ExtractIfNeeded(text, document_type, progress);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 1, Total = 3,
            Message = "Running 27 biochemical agents..."
        });

        var result = await neuroService.JournalAnalyzeAsync(person, extractedText, save);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3, Total = 3,
            Message = $"Complete — {result.Decisions.Count} chemicals detected"
        });

        return JsonSerializer.Serialize(new
        {
            person,
            source_type = "journal",
            decisions_count = result.Decisions.Count,
            decisions = result.Decisions.Select(d => new { d.Signal, d.Formula }),
            synthesis = result.Synthesis
        }, IndentedJson);
    }

    private static string ExtractIfNeeded(string text, string? documentType, IProgress<ProgressNotificationValue>? progress)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return text;

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 0, Total = 3,
            Message = $"Extracting text from {documentType.ToUpperInvariant()} file..."
        });

        return DocumentExtractor.ExtractText(text, documentType);
    }
}
