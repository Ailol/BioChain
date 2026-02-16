using System.Globalization;
using Microsoft.Extensions.AI;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    AnalyzedDataRepository analyzedDataRepo,
    ProfileRepository profileRepo)
{
    public async Task<(int AnalyzedData, int Profiles)> BackfillAsync(string? person = null)
    {
        // 1. Backfill analyzed_data embeddings
        var adEntries = await analyzedDataRepo.GetWithoutEmbeddingsAsync(person);
        var adCount = 0;
        foreach (var (id, content) in adEntries)
        {
            var embedding = await embeddingGenerator.GenerateAsync(content);
            var vector = FormatVector(embedding.Vector.Span);
            await analyzedDataRepo.UpdateEmbeddingAsync(id, vector);
            adCount++;
        }

        // 2. Backfill biochemical_profile embeddings (from reasoning text)
        var profileEntries = await profileRepo.GetWithoutEmbeddingsAsync(person);
        var profileCount = 0;
        foreach (var (id, reasoning) in profileEntries)
        {
            var embedding = await embeddingGenerator.GenerateAsync(reasoning);
            var vector = FormatVector(embedding.Vector.Span);
            await profileRepo.UpdateEmbeddingAsync(id, vector);
            profileCount++;
        }

        return (adCount, profileCount);
    }

    public async Task<string> GenerateVectorAsync(string text)
    {
        var embedding = await embeddingGenerator.GenerateAsync(text);
        return FormatVector(embedding.Vector.Span);
    }

    private static string FormatVector(ReadOnlySpan<float> values)
    {
        var parts = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
            parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
        return $"[{string.Join(",", parts)}]";
    }
}
