using Models;
using Repository;

namespace Agents;

/// <summary>
/// Vector analysis service for personality traits.
/// Provides semantic clustering, nearest-neighbor relationships,
/// NT centroid analysis, and hormone-trait heatmaps.
/// </summary>
public class VectorService
{
    private readonly EmbeddingRepository _embeddingRepo;

    public VectorService(EmbeddingRepository embeddingRepo)
    {
        _embeddingRepo = embeddingRepo;
    }

    /// <summary>
    /// Greedy agglomerative clustering: pick unclustered trait, find all traits
    /// with similarity >= threshold, form cluster. Repeat until all clustered.
    /// </summary>
    public List<TraitCluster> ClusterTraits(List<TraitWithEmbedding> traits, double threshold = 0.75)
    {
        var clusters = new List<TraitCluster>();
        var assigned = new HashSet<int>();

        for (int i = 0; i < traits.Count; i++)
        {
            if (assigned.Contains(i)) continue;

            var clusterIndices = new List<int> { i };
            assigned.Add(i);

            for (int j = i + 1; j < traits.Count; j++)
            {
                if (assigned.Contains(j)) continue;

                var sim = EmbeddingService.CosineSimilarity(traits[i].Embedding, traits[j].Embedding);
                if (sim >= threshold)
                {
                    clusterIndices.Add(j);
                    assigned.Add(j);
                }
            }

            var clusterTraits = clusterIndices.Select(idx => traits[idx]).ToList();
            var label = clusterTraits.OrderByDescending(t => t.Topic.Length).First().Topic;
            var dominantNt = clusterTraits
                .GroupBy(t => t.Neurotransmitter)
                .OrderByDescending(g => g.Count())
                .First().Key;

            clusters.Add(new TraitCluster(label, clusterTraits.Select(t => t.Topic).ToList(), dominantNt));
        }

        return clusters.OrderByDescending(c => c.Traits.Count).ToList();
    }

    /// <summary>
    /// For each trait, find the k nearest neighbors by cosine similarity.
    /// </summary>
    public List<TraitNeighbors> FindTraitNeighbors(List<TraitWithEmbedding> traits, int k = 3)
    {
        var results = new List<TraitNeighbors>();

        for (int i = 0; i < traits.Count; i++)
        {
            var neighbors = new List<SimilarTrait>();

            for (int j = 0; j < traits.Count; j++)
            {
                if (i == j) continue;
                var sim = EmbeddingService.CosineSimilarity(traits[i].Embedding, traits[j].Embedding);
                neighbors.Add(new SimilarTrait(traits[j].Topic, sim));
            }

            var topK = neighbors.OrderByDescending(n => n.Similarity).Take(k).ToList();
            results.Add(new TraitNeighbors(traits[i].Topic, topK));
        }

        return results;
    }

    /// <summary>
    /// Group traits by NT, compute centroid per group, and measure cohesion
    /// (average similarity of each trait to its group centroid).
    /// </summary>
    public List<NtCentroidAnalysis> ComputeNtCentroids(List<TraitWithEmbedding> traits)
    {
        var results = new List<NtCentroidAnalysis>();
        var groups = traits.GroupBy(t => t.Neurotransmitter);

        foreach (var group in groups)
        {
            var embeddings = group.Select(t => t.Embedding).ToList();
            var centroid = EmbeddingService.AggregateEmbeddings(embeddings);

            if (centroid.Length == 0) continue;

            var cohesion = embeddings.Average(e => EmbeddingService.CosineSimilarity(e, centroid));

            results.Add(new NtCentroidAnalysis(group.Key, embeddings.Count, cohesion));
        }

        return results.OrderByDescending(r => r.TraitCount).ToList();
    }

    /// <summary>
    /// For each hormone/peptide, compute similarity to each trait and return
    /// overall strength (avg top-5) plus top contributing traits.
    /// </summary>
    public async Task<List<HormoneTraitHeatmap>> ComputeHeatmapAsync(
        List<TraitWithEmbedding> traits, string table)
    {
        var targets = await _embeddingRepo.GetTargetEmbeddingsAsync(table);
        if (targets.Count == 0) return [];

        var results = new List<HormoneTraitHeatmap>();

        foreach (var (name, targetEmbedding) in targets)
        {
            var contributions = traits
                .Select(t => new TraitContribution(t.Topic, EmbeddingService.CosineSimilarity(t.Embedding, targetEmbedding)))
                .OrderByDescending(c => c.Similarity)
                .ToList();

            var topContributors = contributions.Take(5).ToList();
            var strength = (float)Math.Clamp(topContributors.Average(c => c.Similarity), 0, 1);

            results.Add(new HormoneTraitHeatmap(name, strength, topContributors));
        }

        return results.OrderByDescending(r => r.OverallStrength).ToList();
    }
}
