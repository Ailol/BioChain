using System.Globalization;
using Microsoft.Extensions.AI;
using BioChain.Models;
using BioChain.Repository;

namespace BioChain.Service;

public class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    AgentConfiguration agentConfig,
    AnalyzedDataRepository analyzedDataRepo,
    ObservationRepository observationRepo)
{
    private readonly EmbeddingGenerationOptions? _options =
        agentConfig.Embedding?.Dimensions is { } dim
            ? new EmbeddingGenerationOptions { Dimensions = dim }
            : null;

    public async Task<(int AnalyzedData, int Observations)> BackfillAsync(string? person = null)
    {
        // 1. Backfill analyzed_data embeddings
        var adEntries = await analyzedDataRepo.GetWithoutEmbeddingsAsync(person);
        var adCount = 0;
        foreach (var (id, content) in adEntries)
        {
            var vector = await EmbedAsync(content);
            await analyzedDataRepo.UpdateEmbeddingAsync(id, vector);
            adCount++;
        }

        // 2. Backfill observation embeddings (from formula text)
        var observationEntries = await observationRepo.GetWithoutEmbeddingsAsync(person);
        var observationCount = 0;
        foreach (var (id, formula) in observationEntries)
        {
            var vector = await EmbedAsync(formula);
            await observationRepo.UpdateEmbeddingAsync(id, vector);
            observationCount++;
        }

        return (adCount, observationCount);
    }

    public async Task<string> GenerateVectorAsync(string text) => await EmbedAsync(text);

    private async Task<string> EmbedAsync(string text)
    {
        var result = await embeddingGenerator.GenerateAsync([text], _options);
        return FormatVector(result[0].Vector.Span);
    }

    private static string FormatVector(ReadOnlySpan<float> values)
    {
        var parts = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
            parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
        return $"[{string.Join(",", parts)}]";
    }
}
