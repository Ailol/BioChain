using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Repository;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class EmbeddingTools(AnalyzedDataRepository analyzedDataRepo)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "backfill_embeddings")]
    [Description("Generate vector embeddings for analyzed data entries that don't have them yet. " +
                 "Required before neurorespond will work with vector scoring.")]
    public async Task<string> BackfillEmbeddings(
        [Description("Optional: limit to specific person")] string? person = null)
    {
        var entries = await analyzedDataRepo.GetWithoutEmbeddingsAsync(person);
        // TODO: wire embedding generation when EmbeddingService is rebuilt
        return JsonSerializer.Serialize(new
        {
            pendingEntries = entries.Count,
            note = "Embedding generation not yet wired — entries listed only"
        }, IndentedJson);
    }
}
