using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Service;
using NeuroGateway.Utils;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class PersonalityTools(PersonalityService svc, AnalyseService analyseService, PersonRepository personRepo, PersonalityRepository personalityRepo)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "list_persons")]
    [Description("List all persons in the personality system.")]
    public async Task<string> ListPersons()
    {
        var persons = await personRepo.ListPersonsAsync();
        return JsonSerializer.Serialize(new { persons });
    }

    [McpServerTool(Name = "create_personality")]
    [Description("Create a new person in the personality system.")]
    public async Task<string> CreatePersonality([Description("Person name")] string name)
    {
        var created = await personRepo.CreatePersonAsync(name);
        return created ? $"{{\"created\":\"{name}\"}}" : $"{{\"exists\":\"{name}\"}}";
    }

    [McpServerTool(Name = "get_personality")]
    [Description("Get personality profile with behavior traits and neurotransmitter mappings.")]
    public async Task<string> GetPersonality([Description("Person name")] string person)
    {
        var result = await personalityRepo.GetPersonalityAsync(person);
        if (result.Profile is not null)
        {
            var output = new
            {
                person = result.Profile.Person,
                communicationStyle = result.Profile.CommunicationStyle,
                entries = result.Profile.Entries.Select(e => new
                {
                    content = e.Content,
                    sourceType = e.SourceType,
                    neurotransmitters = e.Neurotransmitters,
                    hormones = e.Hormones,
                    peptides = e.Peptides
                })
            };
            return JsonSerializer.Serialize(output, IndentedJson);
        }

        return result.Suggestions is { Count: > 0 }
            ? JsonSerializer.Serialize(new { error = "Not found", suggestions = result.Suggestions })
            : "{\"error\":\"Not found\"}";
    }

    [McpServerTool(Name = "full_personality_scan")]
    [Description("Get full personality scan including traits, related hormones and peptides based on neurotransmitter interactions.")]
    public async Task<string> FullPersonalityScan([Description("Person name")] string person)
    {
        var scan = await svc.GetFullPersonalityScanAsync(person);
        if (scan is null)
            return "{\"error\":\"Not found\"}";

        var output = new
        {
            person = scan.Person,
            communicationStyle = scan.CommunicationStyle,
            entries = scan.Entries.Select(e => new
            {
                content = e.Content,
                sourceType = e.SourceType,
                neurotransmitters = e.Neurotransmitters,
                hormones = e.Hormones,
                peptides = e.Peptides
            }),
            neurotransmitters = scan.Neurotransmitters,
            hormones = scan.Hormones,
            peptides = scan.Peptides,
            traitClusters = scan.TraitClusters,
            traitRelationships = scan.TraitRelationships,
            ntCentroids = scan.NtCentroids,
            hormoneHeatmap = scan.HormoneHeatmap,
            analysis = scan.Analysis
        };
        return JsonSerializer.Serialize(output, IndentedJson);
    }

    [McpServerTool(Name = "update_personality")]
    [Description("Submit a behavior/event for the NeuroGroupChat to evaluate. Each neurotransmitter agent decides if it's relevant to them and adds their own explanation if so. You do NOT decide - the agents do.")]
    public async Task<string> UpdatePersonality(
        [Description("Person name")] string person,
        [Description("Topic/event name (e.g., Programming, Morning Routine)")] string topic,
        [Description("Context/description of the behavior or event")] string context,
        [Description("Generate embeddings for traits (default true). Set false to skip embeddings and backfill later.")] bool embeddings = true)
    {
        var content = $"{topic}: {context}";
        var result = await svc.AnalyzeAsync(person, content, "manual", embeddings: embeddings);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "analyze_conversation_file")]
    [Description("Analyze a conversation file (txt, WhatsApp, Discord, CSV) to extract personality traits. " +
                 "Identifies important exchanges, differentiates target personality from user (Ailo), " +
                 "and optionally creates personality via NeuroAgent evaluation.")]
    public async Task<string> AnalyzeConversationFile(
        [Description("Raw content of the conversation file")] string fileContent,
        [Description("Name of person being analyzed (the personality)")] string targetPersonalityName,
        [Description("Name of user in conversation (default: Ailo)")] string userName = "Ailo",
        [Description("Format hint: WhatsApp, Discord, CSV, PlainText (auto-detected if not provided)")] string? formatHint = null,
        [Description("Auto-add traits to personality via NeuroAgent evaluation")] bool autoAdd = false)
    {
        try
        {
            ConversationFormat? format = null;
            if (!string.IsNullOrEmpty(formatHint) &&
                Enum.TryParse<ConversationFormat>(formatHint, true, out var parsed))
                format = parsed;

            var request = new DataAnalysisRequest(fileContent, targetPersonalityName,
                UserName: userName, FormatHint: format, AutoAdd: autoAdd);

            var result = await analyseService.AnalyzeDataAsync(request);
            return JsonSerializer.Serialize(result, IndentedJson);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(Name = "analyze_document_file")]
    [Description("Analyze a DOCX or PDF document to extract personality traits. " +
                 "Accepts base64-encoded file content. Extracts text, creates person if needed, " +
                 "runs neuro analysis on each trait, and persists to personality profile. " +
                 "For CV/resume analysis, set useCVAgents=true to use specialized agents that read between the lines of professional language.")]
    public async Task<string> AnalyzeDocumentFile(
        [Description("Base64-encoded file content")] string base64Content,
        [Description("Document type: docx or pdf")] string documentType,
        [Description("Name of person being analyzed (the personality)")] string targetPersonalityName,
        [Description("Generate embeddings for traits (default true). Set false to skip embeddings and backfill later.")] bool embeddings = true,
        [Description("Use CV/resume specialized agents (default false)")] bool useCVAgents = false)
    {
        try
        {
            var docType = documentType.ToLowerInvariant();
            var bytes = Convert.FromBase64String(base64Content);

            var text = docType switch
            {
                "docx" => ParseService.ExtractTextFromDocx(bytes),
                "pdf" => ParseService.ExtractTextFromPdf(bytes),
                _ => throw new ArgumentException($"Unsupported document type: {documentType}. Use 'docx' or 'pdf'.")
            };

            if (string.IsNullOrWhiteSpace(text))
                return JsonSerializer.Serialize(new { error = "No text could be extracted from the document" });

            var request = new DataAnalysisRequest(text, targetPersonalityName,
                DocumentType: docType, Embeddings: embeddings);
            var result = await analyseService.AnalyzeDataAsync(request);
            return JsonSerializer.Serialize(result, IndentedJson);
        }
        catch (FormatException)
        {
            return JsonSerializer.Serialize(new { error = "Invalid base64 content" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
