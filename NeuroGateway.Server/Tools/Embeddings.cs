using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class EmbeddingTools(EmbeddingService embeddingService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "backfill_embeddings")]
    [Description("Generate vector embeddings for analyzed data entries that don't have them yet. " +
                 "Required before vector scoring and similarity search will work.")]
    public async Task<string> BackfillEmbeddings(
        [Description("Optional: limit to specific person")] string? person = null)
    {
        var count = await embeddingService.BackfillAsync(person);
        return JsonSerializer.Serialize(new
        {
            embeddingsGenerated = count,
            message = count > 0 ? $"Generated {count} embedding(s)" : "No entries pending embeddings"
        }, IndentedJson);
    }
}
