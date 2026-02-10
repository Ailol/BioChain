namespace NeuroGateway.Models;

public record AnalyzedDataWithEmbedding(string Content, string SourceType, List<string> Neurotransmitters, float[] Embedding, int AnalyzedDataId);

// Trait Clusters - semantically grouped analyzed data
public record TraitCluster(string Label, List<string> Entries, List<string> Neurotransmitters);

// Trait Relationships - nearest neighbors per entry
public record TraitNeighbors(string Entry, List<SimilarTrait> Neighbors);
public record SimilarTrait(string Entry, double Similarity);

// NT Centroids - centroid-based NT ranking
public record NtCentroidAnalysis(string Neurotransmitter, int TraitCount, double CohesionScore);

// Hormone-Trait Heatmap - per-hormone breakdown showing which entries drive each score
public record HormoneTraitHeatmap(string Name, float OverallStrength, List<TraitContribution> TopContributors);
public record TraitContribution(string Entry, double Similarity);
