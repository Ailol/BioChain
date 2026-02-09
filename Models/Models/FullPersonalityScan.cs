
namespace Models;

public record FullPersonalityScan(
    string Person,
    List<Trait> Traits,
    List<ChemicalScore> Hormones,
    List<ChemicalScore> Peptides,
    List<TraitCluster>? TraitClusters = null,
    List<TraitNeighbors>? TraitRelationships = null,
    List<NtCentroidAnalysis>? NtCentroids = null,
    List<HormoneTraitHeatmap>? HormoneHeatmap = null
);
