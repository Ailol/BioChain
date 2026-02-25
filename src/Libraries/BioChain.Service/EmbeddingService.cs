using System.Globalization;
using Microsoft.Extensions.AI;
using BioChain.Repository;

namespace BioChain.Service;

public class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    AnalyzedDataRepository analyzedDataRepo,
    ObservationRepository observationRepo)
{
    public async Task<(int AnalyzedData, int Observations)> BackfillAsync(string? person = null)
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

        // 2. Backfill observation embeddings (from formula text)
        var observationEntries = await observationRepo.GetWithoutEmbeddingsAsync(person);
        var observationCount = 0;
        foreach (var (id, formula) in observationEntries)
        {
            var embedding = await embeddingGenerator.GenerateAsync(formula);
            var vector = FormatVector(embedding.Vector.Span);
            await observationRepo.UpdateEmbeddingAsync(id, vector);
            observationCount++;
        }

        return (adCount, observationCount);
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
