using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Agents;
using Models;

namespace McpAgentServer.Tools;

/// <summary>
/// MCP tools for neuroresponse queries using vector similarity.
/// </summary>
[McpServerToolType]
public class NeuroTools(NeuroService neuroService, GroupAgentService agentService, Agents.PersonalityService personalityService, EmbeddingService embeddingService)
{
    [McpServerTool(Name = "neurorespond")]
    [Description("Analyze what someone (e.g., Karolina, Anja) wrote TO you. " +
                 "Does a full neuroscan: neurotransmitters, hormones, peptides. " +
                 "Analyzes their state, then crafts suggested responses for YOU to send back. " +
                 "Example: 'neurorespond to karolina, relationship: Dating, text: I had a great time yesterday 😊'")]
    public async Task<string> Neurorespond(
        [Description("Person name who SENT the message (e.g., Karolina, Anja). Their personality will be scanned.")] string person,
        [Description("What they wrote to you — the message you received")] string text,
        [Description("Relationship context keyword (e.g., Dating, Friend, Colleague). " +
                     "Auto-created in DB if not present. Maps to closest group for response generation.")]
        string? relationship = null)
    {
        try
        {
            var result = await neuroService.NeuroAnalyzeAsync(
                person,
                text,
                relationship,
                agentService,
                personalityService);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "backfill_embeddings")]
    [Description("Generate vector embeddings for all personality traits that don't have them yet. " +
                 "This is required before neurorespond will work. Optionally filter by person name.")]
    public async Task<string> BackfillEmbeddings(
        [Description("Optional: limit to specific person (e.g., Karolina)")] string? person = null)
    {
        try
        {
            var result = await embeddingService.BackfillEmbeddingsAsync(person);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(Name = "backfill_hormone_peptide_embeddings")]
    [Description("Generate vector embeddings for all hormone and peptide descriptions that don't have them yet. " +
                 "This is required before full_personality_scan will use vector-computed hormone/peptide scores.")]
    public async Task<string> BackfillHormonePeptideEmbeddings()
    {
        try
        {
            var result = await embeddingService.BackfillHormonePeptideEmbeddingsAsync();
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
