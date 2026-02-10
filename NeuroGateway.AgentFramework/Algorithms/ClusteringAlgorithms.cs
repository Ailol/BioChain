using NeuroGateway.Models;

namespace NeuroGateway.AgentFramework.Algorithms;

/// <summary>
/// Reusable clustering algorithms for analyzed data analysis.
/// Extracted from VectorService/ClusterService.
/// </summary>
public static class ClusteringAlgorithms
{
    /// <summary>
    /// Greedy agglomerative clustering: pick unclustered item, find all items
    /// with similarity >= threshold, form cluster. Repeat until all clustered.
    /// Collects ALL neurotransmitters from all entries in the cluster.
    /// </summary>
    public static List<TraitCluster> GreedyAgglomerative(
        List<AnalyzedDataWithEmbedding> entries, double threshold = 0.75)
    {
        var clusters = new List<TraitCluster>();
        var assigned = new HashSet<int>();

        for (int i = 0; i < entries.Count; i++)
        {
            if (assigned.Contains(i)) continue;

            var clusterIndices = new List<int> { i };
            assigned.Add(i);

            for (int j = i + 1; j < entries.Count; j++)
            {
                if (assigned.Contains(j)) continue;

                var sim = VectorAlgorithms.CosineSimilarity(entries[i].Embedding, entries[j].Embedding);
                if (sim >= threshold)
                {
                    clusterIndices.Add(j);
                    assigned.Add(j);
                }
            }

            var clusterEntries = clusterIndices.Select(idx => entries[idx]).ToList();
            var label = clusterEntries.OrderByDescending(e => e.Content.Length).First().Content;
            var allNts = clusterEntries
                .SelectMany(e => e.Neurotransmitters)
                .Distinct()
                .OrderBy(nt => nt)
                .ToList();

            clusters.Add(new TraitCluster(label, clusterEntries.Select(e => e.Content).ToList(), allNts));
        }

        return clusters.OrderByDescending(c => c.Entries.Count).ToList();
    }

    /// <summary>
    /// For each item, find the k nearest neighbors by cosine similarity.
    /// </summary>
    public static List<TraitNeighbors> FindNeighbors(List<AnalyzedDataWithEmbedding> entries, int k = 3)
    {
        var results = new List<TraitNeighbors>();

        for (int i = 0; i < entries.Count; i++)
        {
            var neighbors = new List<SimilarTrait>();

            for (int j = 0; j < entries.Count; j++)
            {
                if (i == j) continue;
                var sim = VectorAlgorithms.CosineSimilarity(entries[i].Embedding, entries[j].Embedding);
                neighbors.Add(new SimilarTrait(entries[j].Content, sim));
            }

            var topK = neighbors.OrderByDescending(n => n.Similarity).Take(k).ToList();
            results.Add(new TraitNeighbors(entries[i].Content, topK));
        }

        return results;
    }

    /// <summary>
    /// Group entries by each neurotransmitter (an entry with multiple NTs appears in multiple groups),
    /// compute centroid per NT group, and measure cohesion.
    /// </summary>
    public static List<NtCentroidAnalysis> ComputeCentroids(List<AnalyzedDataWithEmbedding> entries)
    {
        var ntGroups = new Dictionary<string, List<float[]>>();

        foreach (var entry in entries)
        {
            foreach (var nt in entry.Neurotransmitters)
            {
                if (!ntGroups.ContainsKey(nt))
                    ntGroups[nt] = [];
                ntGroups[nt].Add(entry.Embedding);
            }
        }

        var results = new List<NtCentroidAnalysis>();
        foreach (var (nt, embeddings) in ntGroups)
        {
            var centroid = VectorAlgorithms.MeanPool(embeddings);
            if (centroid.Length == 0) continue;

            var cohesion = embeddings.Average(e => VectorAlgorithms.CosineSimilarity(e, centroid));
            results.Add(new NtCentroidAnalysis(nt, embeddings.Count, cohesion));
        }

        return results.OrderByDescending(r => r.TraitCount).ToList();
    }
}
