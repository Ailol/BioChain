using NeuroGateway.AnalysisFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using static NeuroGateway.AnalysisFramework.DimensionDefinitions;

namespace NeuroGateway.Service;

/// <summary>
/// Shadow-anchored dimension scoring.
/// Compares person's biochemical reasoning embeddings against 5-level shadow profiles
/// to estimate activation levels, then aggregates across chemicals per dimension.
/// </summary>
public class DimensionService(
    ProfileRepository profileRepo,
    ShadowAnchorService shadowAnchor,
    DimensionDefinitionsService dimDefs)
{
    private const float DecayLambda = 0.01f;       // half-life ~69 days
    private const float FloorPct = 15f;
    private const float CeilPct = 95f;
    private const int ChemicalWeightCap = 5;
    private const float SigmoidThreshold = 3f;

    public async Task<List<DimensionScore>> ScoreAsync(string person, ScoringMode mode = ScoringMode.Work)
    {
        var all = await dimDefs.GetAllAsync();
        var entries = await profileRepo.GetProfileEntriesAsync(person);
        if (entries.Count == 0)
            return all
                .Select(d => new DimensionScore(d.Name, d.Section, d.Category, 0, 0f, 0f, 0, []))
                .ToList();

        var modeStr = mode == ScoringMode.Work ? "work" : "private";

        // Score all dimensions (raw levels)
        var rawResults = new List<(DimensionScore Score, float RawLevel)>(all.Count);

        var chemicalToLayer = await dimDefs.GetChemicalToLayerAsync();

        foreach (var dim in all)
        {
            var result = await ScoreDimensionAsync(dim, entries, modeStr, chemicalToLayer);
            var modeMultiplier = dim.GetModeMultiplier(mode);
            rawResults.Add((result.Score, result.CombinedLevel * modeMultiplier));
        }

        // Min-max rescale per section to 15-95 range
        var results = new List<DimensionScore>(rawResults.Count);

        foreach (var section in new[] { "work", "private" })
        {
            var sectionItems = rawResults.Where(r => r.Score.Section == section).ToList();
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

    /// <summary>
    /// Build the shadow level matrix: for each dimension × chemical pair,
    /// compute the recency-weighted mean shadow level (embedding-only).
    /// Returns a sparse list of cells (~160 data points).
    /// </summary>
    public async Task<ShadowMatrixResponse> GetShadowMatrixAsync(
        string person, ScoringMode mode = ScoringMode.Work)
    {
        var modeStr = mode == ScoringMode.Work ? "work" : "private";

        var all = await dimDefs.GetAllAsync();
        var chemicalToLayer = await dimDefs.GetChemicalToLayerAsync();

        var dimensions = all.Select(d => d.Name).ToList();
        var chemicals = chemicalToLayer.Keys
            .OrderBy(c => chemicalToLayer[c] switch
            {
                "neurotransmitter" => 0, "hormone" => 1, "peptide" => 2, _ => 3
            })
            .ToList();

        var entries = await profileRepo.GetProfileEntriesAsync(person);
        if (entries.Count == 0)
            return new ShadowMatrixResponse(person, modeStr, [], dimensions, chemicals);

        var cells = new List<ShadowMatrixCell>();
        var now = DateTime.UtcNow;

        foreach (var dim in all)
        {
            var shadowChemicals = ShadowProfileLoader.GetChemicalsForDimension(dim.Name, modeStr);
            var relevantChemicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in shadowChemicals) relevantChemicals.Add(c);
            foreach (var c in dim.ChemicalAffinity.Keys) relevantChemicals.Add(c);

            var groups = entries
                .Where(e => relevantChemicals.Contains(e.Chemical))
                .GroupBy(e => e.Chemical, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var chemical = group.Key;
                var layer = chemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";

                float weightedSum = 0, weightTotal = 0;
                foreach (var entry in group)
                {
                    var levelEmb = await shadowAnchor.EstimateLevelAsync(
                        dim.Name, modeStr, chemical, entry.Embedding);
                    var daysSince = (float)(now - entry.CreatedAt).TotalDays;
                    var recency = MathF.Exp(-DecayLambda * daysSince);
                    weightedSum += levelEmb * recency;
                    weightTotal += recency;
                }

                var shadowLevel = weightTotal > 0 ? weightedSum / weightTotal : 3f;
                var confidence = Sigmoid(group.Count(), SigmoidThreshold);

                cells.Add(new ShadowMatrixCell(
                    dim.Name, dim.Section, chemical, layer,
                    MathF.Round(shadowLevel, 2),
                    MathF.Round(confidence, 2),
                    group.Count()));
            }
        }

        return new ShadowMatrixResponse(person, modeStr, cells, dimensions, chemicals);
    }

    private async Task<(DimensionScore Score, float CombinedLevel)> ScoreDimensionAsync(
        DimensionDef dim, List<ProfileRepository.ProfileEntry> allEntries, string mode,
        IReadOnlyDictionary<string, string> chemicalToLayer)
    {
        // Get chemicals relevant to this dimension from shadow profiles
        var shadowChemicals = ShadowProfileLoader.GetChemicalsForDimension(dim.Name, mode);
        // Also include chemicals from the dimension's affinity list
        var relevantChemicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in shadowChemicals) relevantChemicals.Add(c);
        foreach (var c in dim.ChemicalAffinity.Keys) relevantChemicals.Add(c);

        if (relevantChemicals.Count == 0)
            return (new DimensionScore(dim.Name, dim.Section, dim.Category, 0, 0f, 0f, 0, []), 3f);

        // Filter entries to relevant chemicals
        var relevantEntries = allEntries
            .Where(e => relevantChemicals.Contains(e.Chemical))
            .ToList();

        if (relevantEntries.Count == 0)
            return (new DimensionScore(dim.Name, dim.Section, dim.Category, 0, 0f, 0f, 0, []), 3f);

        // Group by chemical
        var chemicalGroups = relevantEntries
            .GroupBy(e => e.Chemical, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var now = DateTime.UtcNow;
        float weightedLevelSum = 0, weightSum = 0;
        float confidenceWeightedSum = 0, confidenceWeightSum = 0;
        var allEntryLevels = new List<float>();
        var allAgreements = new List<float>();
        var evidenceLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidence = new List<DimensionEvidence>();

        // For temporal trajectory: (timestamp, level)
        var temporalPoints = new List<(DateTime Time, float Level)>();
        // For circuit coherence: chemical → list of entry levels
        var chemicalLevelMap = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in chemicalGroups)
        {
            var chemical = group.Key;
            var chemEntries = group.ToList();
            var layer = chemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";
            evidenceLayers.Add(layer);

            // Per-entry scoring
            var entryLevels = new List<(float Level, float Recency)>();
            var chemLevels = new List<float>();

            foreach (var entry in chemEntries)
            {
                // level_emb: cosine sim vs 5 shadow levels → continuous 1.0-5.0
                var levelEmb = await shadowAnchor.EstimateLevelAsync(dim.Name, mode, chemical, entry.Embedding);

                // level_mod: map intensity_factor (-1.0 to +1.0) → level (1 to 5)
                var levelMod = MapIntensityToLevel(entry.IntensityFactor);

                // When intensity_factor is hardcoded 1.0 (maps to level 5.0),
                // use embedding-only level to avoid inflating all scores.
                float entryLevel;
                if (MathF.Abs(entry.IntensityFactor - 1.0f) < 0.001f)
                    entryLevel = levelEmb;
                else
                    entryLevel = (levelMod + levelEmb) / 2f;

                var agreement = 1f - MathF.Abs(levelMod - levelEmb) / 4f;
                allAgreements.Add(agreement);
                allEntryLevels.Add(entryLevel);
                chemLevels.Add(entryLevel);

                // Track temporal data
                temporalPoints.Add((entry.CreatedAt, entryLevel));

                // Recency weight for aggregation
                var daysSince = (float)(now - entry.CreatedAt).TotalDays;
                var recency = MathF.Exp(-DecayLambda * daysSince);
                entryLevels.Add((entryLevel, recency));

                evidence.Add(new DimensionEvidence(chemical, layer, entry.Reasoning, entryLevel, recency));
            }

            chemicalLevelMap[chemical] = chemLevels;

            // chemical_level: recency-weighted mean of entry levels
            float chemLevelSum = 0, chemWeightSum = 0;
            foreach (var (level, recency) in entryLevels)
            {
                chemLevelSum += level * recency;
                chemWeightSum += recency;
            }
            var chemicalLevel = chemWeightSum > 0 ? chemLevelSum / chemWeightSum : 3f;

            // chemical_confidence: sigmoid based on entry count
            var chemicalConfidence = Sigmoid(chemEntries.Count, SigmoidThreshold);

            // chemical_weight: capped to prevent dominance
            var chemicalWeight = MathF.Min(chemEntries.Count, ChemicalWeightCap);

            // Accumulate for dimension-level aggregation
            weightedLevelSum += chemicalLevel * chemicalWeight;
            weightSum += chemicalWeight;

            confidenceWeightedSum += chemicalConfidence * chemicalWeight;
            confidenceWeightSum += chemicalWeight;
        }

        // Dimension aggregation
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

        // Temporal trajectory
        var trajectory = ComputeTrajectory(temporalPoints);

        // Circuit coherence
        var circuit = ComputeCircuitCoherence(chemicalLevelMap);

        var score = new DimensionScore(
            dim.Name, dim.Section, dim.Category, 0,
            confidence, consistency, evidence.Count, evidence,
            trajectory, circuit);

        return (score, combinedLevel);
    }

    /// <summary>Map intensity_factor (-1.0 to +1.0) → level (1.0 to 5.0)</summary>
    private static float MapIntensityToLevel(float intensityFactor)
        => Math.Clamp((intensityFactor + 1f) * 2f + 1f, 1f, 5f);

    /// <summary>Sigmoid: 1 / (1 + exp(-(count - threshold)))</summary>
    private static float Sigmoid(int count, float threshold)
        => 1f / (1f + MathF.Exp(-(count - threshold)));

    /// <summary>
    /// Linear regression over timestamped activation levels.
    /// Returns slope (level-change/day), direction label, R², and boundary levels.
    /// </summary>
    private static TemporalTrajectory? ComputeTrajectory(List<(DateTime Time, float Level)> points)
    {
        if (points.Count < 3) return null;

        var sorted = points.OrderBy(p => p.Time).ToList();
        var t0 = sorted[0].Time;

        // Convert to (days_since_first, level)
        var xs = sorted.Select(p => (float)(p.Time - t0).TotalDays).ToArray();
        var ys = sorted.Select(p => p.Level).ToArray();
        var n = xs.Length;

        // Linear regression: y = slope * x + intercept
        var xMean = xs.Average();
        var yMean = ys.Average();

        float ssXY = 0, ssXX = 0, ssTot = 0, ssRes = 0;
        for (var i = 0; i < n; i++)
        {
            ssXY += (xs[i] - xMean) * (ys[i] - yMean);
            ssXX += (xs[i] - xMean) * (xs[i] - xMean);
        }

        var slope = ssXX > 0 ? ssXY / ssXX : 0f;
        var intercept = yMean - slope * xMean;

        // R²
        for (var i = 0; i < n; i++)
        {
            var predicted = slope * xs[i] + intercept;
            ssRes += (ys[i] - predicted) * (ys[i] - predicted);
            ssTot += (ys[i] - yMean) * (ys[i] - yMean);
        }
        var r2 = ssTot > 0 ? Math.Clamp(1f - ssRes / ssTot, 0f, 1f) : 0f;

        // Direction label based on slope magnitude
        // Slope is level-change per day; 0.01 = ~0.3 levels/month
        var direction = MathF.Abs(slope) < 0.005f ? "Stable"
            : slope > 0.02f ? "Rising Sharply"
            : slope > 0 ? "Rising"
            : slope < -0.02f ? "Declining Sharply"
            : "Declining";

        return new TemporalTrajectory(
            MathF.Round(slope, 5),
            direction,
            MathF.Round(r2, 3),
            n,
            ys[0],
            ys[^1]);
    }

    /// <summary>
    /// Compute chemical interaction graph for a dimension.
    /// Measures pairwise correlation between chemicals' mean levels.
    /// </summary>
    private static CircuitCoherence? ComputeCircuitCoherence(
        Dictionary<string, List<float>> chemicalLevelMap)
    {
        var chemicals = chemicalLevelMap.Keys.ToList();
        if (chemicals.Count < 2) return null;

        // Compute mean level per chemical
        var means = chemicals.ToDictionary(
            c => c, c => chemicalLevelMap[c].Average(),
            StringComparer.OrdinalIgnoreCase);

        var edges = new List<ChemicalEdge>();
        var coherenceValues = new List<float>();

        // Pairwise: compare whether chemicals agree on level direction
        for (var i = 0; i < chemicals.Count; i++)
        {
            for (var j = i + 1; j < chemicals.Count; j++)
            {
                var a = chemicals[i];
                var b = chemicals[j];
                var levelsA = chemicalLevelMap[a];
                var levelsB = chemicalLevelMap[b];

                // Compute correlation if both have multiple entries
                float correlation;
                if (levelsA.Count >= 2 && levelsB.Count >= 2)
                {
                    // Use mean-level agreement: how similar their activation levels are
                    var diff = MathF.Abs(means[a] - means[b]);
                    correlation = 1f - diff / 4f; // -1 to 1 range mapped from 0-4 diff
                }
                else
                {
                    // Single entries: simple level agreement
                    var diff = MathF.Abs(means[a] - means[b]);
                    correlation = 1f - diff / 4f;
                }

                coherenceValues.Add(correlation);

                var relationship = correlation > 0.7f ? "Synergistic"
                    : correlation > 0.3f ? "Cooperative"
                    : correlation > -0.3f ? "Independent"
                    : correlation > -0.7f ? "Opposing"
                    : "Antagonistic";

                edges.Add(new ChemicalEdge(a, b,
                    MathF.Round(correlation, 3), relationship));
            }
        }

        var coherenceScore = coherenceValues.Count > 0
            ? Math.Clamp(coherenceValues.Average(), 0f, 1f) : 0f;

        // Pattern: characterize the overall circuit
        var synCount = edges.Count(e => e.Relationship == "Synergistic");
        var oppCount = edges.Count(e => e.Relationship is "Opposing" or "Antagonistic");
        var pattern = oppCount > edges.Count / 3f ? "Conflicted Circuit"
            : synCount > edges.Count * 2 / 3f ? "Coherent Circuit"
            : synCount > oppCount ? "Mostly Aligned"
            : "Mixed Signals";

        return new CircuitCoherence(
            MathF.Round(coherenceScore, 3),
            edges, pattern);
    }
}
