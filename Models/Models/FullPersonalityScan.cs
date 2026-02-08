
namespace Models;

public record FullPersonalityScan(
    string Person,
    List<Trait> Traits,
    List<Interaction> Hormones,
    List<Interaction> Peptides,
    List<TraitCluster>? TraitClusters = null,
    List<TraitNeighbors>? TraitRelationships = null,
    List<NtCentroidAnalysis>? NtCentroids = null,
    List<HormoneTraitHeatmap>? HormoneHeatmap = null
);

public record Interaction(string Name, float Strength);
