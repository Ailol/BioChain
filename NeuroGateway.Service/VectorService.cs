using NeuroGateway.AgentFramework.Algorithms;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

/// <summary>
/// Vector analysis service for biochemical heatmap computation.
/// </summary>
public class VectorService
{
    private readonly EmbeddingRepository _embeddingRepo;

    public VectorService(EmbeddingRepository embeddingRepo)
    {
        _embeddingRepo = embeddingRepo;
    }

    /// <summary>
    /// For each hormone/peptide, compute similarity to each analyzed entry and return
    /// overall strength (avg top-5) plus top contributing entries.
    /// </summary>
    public async Task<List<HormoneTraitHeatmap>> ComputeHeatmapAsync(
        List<AnalyzedDataWithEmbedding> entries, string table)
    {
        var targets = await _embeddingRepo.GetTargetEmbeddingsAsync(table);
        if (targets.Count == 0) return [];

        var results = new List<HormoneTraitHeatmap>();

        foreach (var (name, targetEmbedding) in targets)
        {
            var contributions = entries
                .Select(e => new TraitContribution(e.Content, VectorAlgorithms.CosineSimilarity(e.Embedding, targetEmbedding)))
                .OrderByDescending(c => c.Similarity)
                .ToList();

            var topContributors = contributions.Take(5).ToList();
            var strength = (float)Math.Clamp(topContributors.Average(c => c.Similarity), 0, 1);

            results.Add(new HormoneTraitHeatmap(name, strength, topContributors));
        }

        return results.OrderByDescending(r => r.OverallStrength).ToList();
    }
}
