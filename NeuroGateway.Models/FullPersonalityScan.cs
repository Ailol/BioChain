namespace NeuroGateway.Models;

public record FullPersonalityScan(
    string Person,
    List<AnalyzedEntry> Entries,
    List<ChemicalScore> Neurotransmitters,
    List<ChemicalScore> Hormones,
    List<ChemicalScore> Peptides,
    List<TraitCluster>? TraitClusters = null,
    List<TraitNeighbors>? TraitRelationships = null,
    List<NtCentroidAnalysis>? NtCentroids = null,
    List<HormoneTraitHeatmap>? HormoneHeatmap = null,
    string? CommunicationStyle = null,
    string? Analysis = null
);
