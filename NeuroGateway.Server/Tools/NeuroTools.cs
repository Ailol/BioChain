using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.AgentFramework;
using NeuroGateway.Repository;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class NeuroTools(NeuroService neuroService, EmbeddingService embeddingService,
    LlmService llm, EmbeddingRepository embeddingRepo, AnalyzedDataRepository analyzedDataRepo)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

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
            var result = await neuroService.NeuroRespondAsync(person, text, relationship);
            return JsonSerializer.Serialize(result, IndentedJson);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "backfill_embeddings")]
    [Description("Generate vector embeddings for all analyzed data entries that don't have them yet. " +
                 "This is required before neurorespond will work. Optionally filter by person name. " +
                 "Also backfills hormone and peptide description embeddings needed for full_personality_scan.")]
    public async Task<string> BackfillEmbeddings(
        [Description("Optional: limit to specific person (e.g., Karolina)")] string? person = null)
    {
        try
        {
            var entries = await analyzedDataRepo.GetWithoutEmbeddingsAsync(person);
            var entryResult = await embeddingService.BackfillAsync(entries,
                e => llm.EmbedAsync(e.Content),
                (e, vec) => analyzedDataRepo.UpdateEmbeddingAsync(e.Id, vec),
                "Analyzed data");

            var items = await embeddingRepo.GetItemsWithoutEmbeddingsAsync();
            var itemResult = await embeddingService.BackfillAsync(items,
                i => llm.EmbedAsync(i.Name),
                (i, vec) => embeddingRepo.UpdateItemEmbeddingAsync(i.Table, i.Id, vec),
                "Hormones/peptides");

            return JsonSerializer.Serialize(new
            {
                analyzedData = entryResult,
                hormonesPeptides = itemResult
            }, IndentedJson);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
