using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Agents;
using Models;
using Repository;

namespace McpAgentServer.Tools;

[McpServerToolType]
public class PersonalityTools(Agents.PersonalityService svc, PersonRepository personRepo, PersonalityRepository personalityRepo)
{
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
            return JsonSerializer.Serialize(result.Profile);

        return result.Suggestions is { Count: > 0 }
            ? JsonSerializer.Serialize(new { error = "Not found", suggestions = result.Suggestions })
            : "{\"error\":\"Not found\"}";
    }

    [McpServerTool(Name = "full_personality_scan")]
    [Description("Get full personality scan including traits, related hormones and peptides based on neurotransmitter interactions.")]
    public async Task<string> FullPersonalityScan([Description("Person name")] string person)
    {
        var result = await svc.GetFullPersonalityScanAsync(person);
        return result is not null
            ? JsonSerializer.Serialize(result)
            : "{\"error\":\"Not found\"}";
    }

    [McpServerTool(Name = "update_personality")]
    [Description("Submit a behavior/event for the NeuroGroupChat to evaluate. Each neurotransmitter agent decides if it's relevant to them and adds their own explanation if so. You do NOT decide - the agents do.")]
    public async Task<string> UpdatePersonality(
        [Description("Person name")] string person,
        [Description("Topic/event name (e.g., Programming, Morning Routine)")] string topic,
        [Description("Context/description of the behavior or event")] string context,
        [Description("Generate embeddings for traits (default true). Set false to skip embeddings and backfill later.")] bool embeddings = true)
        => JsonSerializer.Serialize(await svc.UpdatePersonalityAsync(person, topic, context, embeddings));

    [McpServerTool(Name = "scan_chat_update_personality")]
    [Description("Analyze chat for behavior patterns, optionally auto-add as traits.")]
    public async Task<string> ScanChat(
        [Description("Person name")] string person,
        [Description("Chat JSON: [{\"role\":\"user\",\"content\":\"...\"}]")] string chatJson,
        [Description("Auto-add traits")] bool autoAdd = false)
    {
        var inputs = JsonSerializer.Deserialize<List<ChatMessageInput>>(chatJson);
        if (inputs is not { Count: > 0 })
            return "{\"error\":\"Invalid chat\"}";

        var msgs = inputs.Select(m => new Microsoft.Extensions.AI.ChatMessage(
            new Microsoft.Extensions.AI.ChatRole(m.Role), m.Content)).ToList();
        return JsonSerializer.Serialize(await svc.ScanChatAsync(person, msgs, autoAdd));
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

            var request = new ConversationAnalysisRequest(
                fileContent,
                targetPersonalityName,
                userName,
                format,
                autoAdd);

            var result = await svc.AnalyzeConversationAsync(request);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(Name = "analyze_document_file")]
    [Description("Analyze a DOCX or PDF document to extract personality traits. " +
                 "Accepts base64-encoded file content. Extracts text, creates person if needed, " +
                 "runs neuro analysis on each trait, and persists to personality profile.")]
    public async Task<string> AnalyzeDocumentFile(
        [Description("Base64-encoded file content")] string base64Content,
        [Description("Document type: docx or pdf")] string documentType,
        [Description("Name of person being analyzed (the personality)")] string targetPersonalityName,
        [Description("Generate embeddings for traits (default true). Set false to skip embeddings and backfill later.")] bool embeddings = true)
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

            var result = await svc.AnalyzeDocumentAsync(text, targetPersonalityName, docType, embeddings);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
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

    [McpServerTool(Name = "get_co_occurrences")]
    [Description("Get co-occurrence relationships for a biochemical. Shows which chemicals from other layers appear on the same traits. " +
                 "For example, query Dopamine (neurotransmitter) to see which hormones and peptides co-occur most frequently.")]
    public async Task<string> GetCoOccurrences(
        [Description("Person name")] string person,
        [Description("Chemical name (e.g., Dopamine, Cortisol, Oxytocin)")] string chemical,
        [Description("Layer of the chemical: neurotransmitter, hormone, or peptide")] string layer)
    {
        try
        {
            var normalizedLayer = layer.ToLowerInvariant();
            var validLayers = new[] { "neurotransmitter", "hormone", "peptide" };
            if (!validLayers.Contains(normalizedLayer))
                return JsonSerializer.Serialize(new { error = "Layer must be 'neurotransmitter', 'hormone', or 'peptide'" });

            var otherLayers = validLayers.Where(l => l != normalizedLayer).ToList();

            var tasks = otherLayers.Select(target =>
                personalityRepo.GetCoOccurrencesAsync(person, normalizedLayer, chemical, target));
            var results = await Task.WhenAll(tasks);

            var profile = new CoOccurrenceProfile(
                chemical,
                normalizedLayer,
                normalizedLayer != "neurotransmitter" ? results[otherLayers.IndexOf("neurotransmitter")] : null,
                normalizedLayer != "hormone" ? results[otherLayers.IndexOf("hormone")] : null,
                normalizedLayer != "peptide" ? results[otherLayers.IndexOf("peptide")] : null
            );

            return JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
