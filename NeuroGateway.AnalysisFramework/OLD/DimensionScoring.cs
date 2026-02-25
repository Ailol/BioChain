using NeuroGateway.Models;
using static NeuroGateway.AnalysisFramework.OLD.DimensionDefinitions;

namespace NeuroGateway.AnalysisFramework.OLD;

// Shadow-anchored dimension scoring algorithm.
// Pure logic — takes all data as parameters, no DB access.
// Level estimation is provided via delegate from the service layer.
public class DimensionScoring
{
    private const float DecayLambda = 0.01f;       // half-life ~69 days
    private const float FloorPct = 15f;
    private const float CeilPct = 95f;
    private const int ChemicalWeightCap = 5;
    private const float SigmoidThreshold = 3f;
    private const float DefaultAffinityWeight = 0.5f;
    private const float MaxModulationMagnitude = 0.5f;

    // Delegate for level estimation (implemented by ShadowAnchorService in service layer)
    public delegate Task<float> EstimateLevelFunc(
        string dimension, string mode, string chemical, float[] embedding);

    // Score all dimensions for a person and apply per-section min-max rescaling.
    public async Task<List<DimensionScore>> ScoreAllAsync(
        IReadOnlyList<DimensionDef> dimensions,
        List<ChemicalObservation> entries,
        ScoringMode mode,
        IReadOnlyDictionary<string, string> chemicalToLayer,
        IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)> interactions,
        EstimateLevelFunc estimateLevel)
    {
        if (entries.Count == 0)
            return dimensions
                .Select(d => new DimensionScore(d.Name, d.Section, d.Category, 0, 0f, 0f, 0, []))
                .ToList();

        var modeStr = mode == ScoringMode.Work ? "work" : "private";

        var rawResults = new List<(DimensionScore Score, float RawLevel)>(dimensions.Count);

        foreach (var dim in dimensions)
        {
            var result = await ScoreDimensionAsync(dim, entries, modeStr, chemicalToLayer, interactions, estimateLevel);
            var modeMultiplier = dim.GetModeMultiplier(mode);
            rawResults.Add((result.Score, result.CombinedLevel * modeMultiplier));
        }

        return Rescale(rawResults);
    }

    // Min-max rescale per section to FloorPct-CeilPct range.
    public static List<DimensionScore> Rescale(List<(DimensionScore Score, float RawLevel)> rawResults)
    {
        var results = new List<DimensionScore>(rawResults.Count);

        foreach (var section in new[] { "work", "private" })
        {
            var sectionItems = rawResults.Where(r => r.Score.Section.Equals(section, StringComparison.OrdinalIgnoreCase)).ToList();
            if (sectionItems.Count == 0) continue;

            var sectionLevels = sectionItems.Select(r => r.RawLevel).ToArray();
            var minLevel = sectionLevels.Min();
            var maxLevel = sectionLevels.Max();
            var range = maxLevel - minLevel;

            foreach (var (score, rawLevel) in sectionItems)
            {
                int finalScore;
                if (range < 0.001f)
                    finalScore = (int)MathF.Round(FloorPct + (CeilPct - FloorPct) / 2f);
                else
                {
                    var normalized = (rawLevel - minLevel) / range;
                    finalScore = (int)MathF.Round(FloorPct + normalized * (CeilPct - FloorPct));
                }

                results.Add(score with { Score = finalScore });
            }
        }

        return results;
    }

    private async Task<(DimensionScore Score, float CombinedLevel)> ScoreDimensionAsync(
        DimensionDef dim, List<ChemicalObservation> allEntries, string mode,
        IReadOnlyDictionary<string, string> chemicalToLayer,
        IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)> interactions,
        EstimateLevelFunc estimateLevel)
    {
        // Get chemicals relevant to this dimension from shadow profiles + affinity list
        var shadowChemicals = ShadowProfileLoader.GetChemicalsForDimension(dim.Name, mode);
        var relevantChemicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in shadowChemicals) relevantChemicals.Add(c);
        foreach (var c in dim.ChemicalAffinity.Keys) relevantChemicals.Add(c);

        if (relevantChemicals.Count == 0)
            return (new DimensionScore(dim.Name, dim.Section, dim.Category, 0, 0f, 0f, 0, []), 3f);

        var relevantEntries = allEntries
            .Where(e => relevantChemicals.Contains(e.Chemical))
            .ToList();

        if (relevantEntries.Count == 0)
            return (new DimensionScore(dim.Name, dim.Section, dim.Category, 0, 0f, 0f, 0, []), 3f);

        var chemicalGroups = relevantEntries
            .GroupBy(e => e.Chemical, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var now = DateTime.UtcNow;
        var allEntryLevels = new List<float>();
        var allAgreements = new List<float>();
        var evidenceLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidence = new List<DimensionEvidence>();

        // For temporal trajectory
        var temporalPoints = new List<(DateTime Time, float Level, float[] Embedding)>();
        // For circuit coherence
        var chemicalLevelMap = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);
        var chemicalEmbeddingMap = new Dictionary<string, List<float[]>>(StringComparer.OrdinalIgnoreCase);

        // Per-chemical aggregation: collect levels, weights, confidences
        var chemicalLevels = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var chemicalWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var chemicalConfidences = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in chemicalGroups)
        {
            var chemical = group.Key;
            var chemEntries = group.ToList();
            var layer = chemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";
            evidenceLayers.Add(layer);

            var entryLevels = new List<(float Level, float Recency)>();
            var chemLevels = new List<float>();
            var chemEmbeddings = new List<float[]>();

            foreach (var entry in chemEntries)
            {
                var levelEmb = await estimateLevel(dim.Name, mode, chemical, entry.Embedding);
                var levelMod = LevelEstimator.MapIntensityToLevel(entry.IntensityFactor);

                // When intensity_factor is hardcoded 1.0, use embedding-only to avoid inflation
                float entryLevel;
                if (MathF.Abs(entry.IntensityFactor - 1.0f) < 0.001f)
                    entryLevel = levelEmb;
                else
                    entryLevel = (levelMod + levelEmb) / 2f;

                var agreement = 1f - MathF.Abs(levelMod - levelEmb) / 4f;
                allAgreements.Add(agreement);
                allEntryLevels.Add(entryLevel);
                chemLevels.Add(entryLevel);
                chemEmbeddings.Add(entry.Embedding);

                temporalPoints.Add((entry.CreatedAt, entryLevel, entry.Embedding));

                var daysSince = (float)(now - entry.CreatedAt).TotalDays;
                var recency = MathF.Exp(-DecayLambda * daysSince);
                entryLevels.Add((entryLevel, recency));

                evidence.Add(new DimensionEvidence(chemical, layer, entry.Reasoning, entryLevel, recency));
            }

            chemicalLevelMap[chemical] = chemLevels;
            chemicalEmbeddingMap[chemical] = chemEmbeddings;

            // Recency-weighted mean of entry levels
            float chemLevelSum = 0, chemWeightSum = 0;
            foreach (var (level, recency) in entryLevels)
            {
                chemLevelSum += level * recency;
                chemWeightSum += recency;
            }
            var chemicalLevel = chemWeightSum > 0 ? chemLevelSum / chemWeightSum : 3f;

            var chemicalConfidence = LevelEstimator.Sigmoid(chemEntries.Count, SigmoidThreshold);

            // Affinity-scaled weight: primary drivers (1.0) outweigh secondary contributors (0.4)
            var affinityWeight = dim.ChemicalAffinity.TryGetValue(chemical, out var aw) ? aw : DefaultAffinityWeight;
            var chemicalWeight = MathF.Min(chemEntries.Count, ChemicalWeightCap) * affinityWeight;

            chemicalLevels[chemical] = chemicalLevel;
            chemicalWeights[chemical] = chemicalWeight;
            chemicalConfidences[chemical] = chemicalConfidence;
        }

        // Modulation pass: apply known chemical interactions.
        // Snapshot original levels so modulation doesn't depend on iteration order.
        var originalLevels = new Dictionary<string, float>(chemicalLevels, StringComparer.OrdinalIgnoreCase);

        foreach (var target in chemicalLevels.Keys.ToList())
        {
            var totalMod = 0f;
            foreach (var source in originalLevels.Keys)
            {
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) continue;

                var sourceKey = source.ToLowerInvariant();
                var targetKey = target.ToLowerInvariant();
                if (!interactions.TryGetValue((sourceKey, targetKey), out var interaction)) continue;

                // Normalize source level from 1-5 to 0-1
                var sourceActivity = (originalLevels[source] - 1f) / 4f;
                totalMod += interaction.ModFactor * sourceActivity;
            }

            totalMod = Math.Clamp(totalMod, -MaxModulationMagnitude, MaxModulationMagnitude);
            var adjusted = originalLevels[target] * (1f + totalMod);
            chemicalLevels[target] = Math.Clamp(adjusted, 1f, 5f);
        }

        // Dimension aggregation with modulated levels and affinity-scaled weights
        float weightedLevelSum = 0, weightSum = 0;
        float confidenceWeightedSum = 0, confidenceWeightSum = 0;

        foreach (var chemical in chemicalLevels.Keys)
        {
            weightedLevelSum += chemicalLevels[chemical] * chemicalWeights[chemical];
            weightSum += chemicalWeights[chemical];
            confidenceWeightedSum += chemicalConfidences[chemical] * chemicalWeights[chemical];
            confidenceWeightSum += chemicalWeights[chemical];
        }

        var combinedLevel = weightSum > 0 ? weightedLevelSum / weightSum : 3f;

        // Confidence: multi-factor
        var baseConfidence = confidenceWeightSum > 0 ? confidenceWeightedSum / confidenceWeightSum : 0f;
        var layerBonus = evidenceLayers.Count / 3f;
        var meanAgreement = allAgreements.Count > 0 ? allAgreements.Average() : 0.5f;
        var confidence = baseConfidence * (0.7f + 0.3f * layerBonus) * meanAgreement;
        confidence = Math.Clamp(confidence, 0f, 1f);

        // Consistency: 1 - stddev/2
        float consistency;
        if (allEntryLevels.Count < 2)
            consistency = 1f;
        else
        {
            var mean = allEntryLevels.Average();
            var variance = allEntryLevels.Sum(l => (l - mean) * (l - mean)) / allEntryLevels.Count;
            var stddev = MathF.Sqrt(variance);
            consistency = Math.Clamp(1f - stddev / 2f, 0f, 1f);
        }

        var trajectory = TrajectoryAnalysis.ComputeTrajectory(temporalPoints);
        var circuit = CircuitCoherenceAnalysis.Compute(chemicalLevelMap, chemicalEmbeddingMap, interactions);

        var score = new DimensionScore(
            dim.Name, dim.Section, dim.Category, 0,
            confidence, consistency, evidence.Count, evidence,
            trajectory, circuit);

        return (score, combinedLevel);
    }
}
