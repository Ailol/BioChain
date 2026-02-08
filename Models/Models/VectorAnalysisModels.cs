namespace Models;

public record TraitWithEmbedding(string Topic, string Explanation, string Neurotransmitter, float[] Embedding);

// Trait Clusters - semantically grouped traits
public record TraitCluster(string Label, List<string> Traits, string DominantNt);

// Trait Relationships - nearest neighbors per trait
public record TraitNeighbors(string Trait, List<SimilarTrait> Neighbors);
public record SimilarTrait(string Trait, double Similarity);

// NT Centroids - centroid-based NT ranking
public record NtCentroidAnalysis(string Neurotransmitter, int TraitCount, double CohesionScore);

// Hormone-Trait Heatmap - per-hormone breakdown showing which traits drive each score
public record HormoneTraitHeatmap(string Name, float OverallStrength, List<TraitContribution> TopContributors);
public record TraitContribution(string Trait, double Similarity);
