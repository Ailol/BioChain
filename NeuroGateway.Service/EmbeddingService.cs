using System.Globalization;
using Microsoft.Extensions.AI;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    AnalyzedDataRepository analyzedDataRepo)
{
    public async Task<int> BackfillAsync(string? person = null)
    {
        var entries = await analyzedDataRepo.GetWithoutEmbeddingsAsync(person);
        if (entries.Count == 0) return 0;

        var count = 0;
        foreach (var (id, content) in entries)
        {
            var embedding = await embeddingGenerator.GenerateAsync(content);
            var vector = FormatVector(embedding.Vector.Span);
            await analyzedDataRepo.UpdateEmbeddingAsync(id, vector);
            count++;
        }

        return count;
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
