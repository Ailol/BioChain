using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using BioChain.Service;

namespace BioChain.Server.Tools;

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
        var (adCount, profileCount) = await embeddingService.BackfillAsync(person);
        var total = adCount + profileCount;
        return JsonSerializer.Serialize(new
        {
            analyzed_data_embeddings = adCount,
            profile_embeddings = profileCount,
            total,
            message = total > 0
                ? $"Generated {adCount} analyzed_data + {profileCount} profile embedding(s)"
                : "No entries pending embeddings"
        }, IndentedJson);
    }
}
