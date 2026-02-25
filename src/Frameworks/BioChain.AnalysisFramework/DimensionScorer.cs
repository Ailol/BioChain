using BioChain.Models;

namespace BioChain.AnalysisFramework;

/// <summary>
/// Scores dimensions from observation data using signal affinity weights
/// and embedding-based level estimation.
/// Pure computation — no DB access.
/// </summary>
public static class DimensionScorer
{
    private const float DecayLambda = 0.01f;

    /// <summary>
    /// Score all dimensions for a person given their observations.
    /// </summary>
    /// <param name="dimensions">Dimension definitions with signal affinity weights</param>
    /// <param name="observations">Person's signal observations with embeddings</param>
    /// <param name="mode">Work or Private scoring mode</param>
    /// <param name="signalToLayer">Signal key → layer mapping</param>
    /// <param name="estimateLevel">Async function: (dimension, mode, signal, embedding) → level (1-5)</param>
    public static async Task<List<DimensionScore>> ScoreAsync(
        IReadOnlyList<DimensionDef> dimensions,
        List<SignalObservation> observations,
        ScoringMode mode,
        IReadOnlyDictionary<string, string> signalToLayer,
        Func<string, string, string, float[], Task<float>> estimateLevel)
    {
        var modeStr = mode == ScoringMode.Work ? "work" : "private";
        var now = DateTime.UtcNow;
        var scores = new List<DimensionScore>();

        foreach (var dim in dimensions)
        {
            var relevanceWeight = mode == ScoringMode.Work
                ? dim.WorkRelevance : dim.PrivateRelevance;

            // Find observations for signals that have affinity with this dimension
            var relevantObs = observations
                .Where(o => dim.SignalAffinity.ContainsKey(o.Signal))
                .GroupBy(o => o.Signal, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (relevantObs.Count == 0)
            {
                scores.Add(new DimensionScore(
                    dim.Name, dim.Section, dim.Category,
                    Score: 50, Confidence: 0f, Consistency: 0f, EvidenceCount: 0,
                    Evidence: []));
                continue;
            }

            var evidence = new List<DimensionEvidence>();
            var signalLevels = new List<(float Level, float Weight, float Recency)>();
            var allLevels = new List<float>();

            foreach (var group in relevantObs)
            {
                var signal = group.Key;
                var affinity = dim.SignalAffinity.GetValueOrDefault(signal, 0f);
                if (affinity <= 0) continue;

                var layer = signalToLayer.GetValueOrDefault(signal, "unknown");
                var sorted = group.OrderByDescending(o => o.CreatedAt).ToList();

                float weightedSum = 0f, weightTotal = 0f;

                for (var i = 0; i < sorted.Count; i++)
                {
                    var obs = sorted[i];
                    float level;

                    if (obs.Embedding.Length > 0)
                    {
                        level = await estimateLevel(dim.Name, modeStr, signal, obs.Embedding);
                    }
                    else
                    {
                        // Fallback: use intensity as level proxy
                        level = 1f + obs.IntensityFactor * 4f; // map 0-1 to 1-5
                    }

                    var daysSince = (float)(now - obs.CreatedAt).TotalDays;
                    var recency = MathF.Exp(-DecayLambda * daysSince);
                    var halvingWt = ResistanceEngine.HalvingWeight(i);
                    var combinedWeight = recency * halvingWt * affinity;

                    weightedSum += level * combinedWeight;
                    weightTotal += combinedWeight;
                    allLevels.Add(level);

                    // Keep top evidence entries
                    if (evidence.Count < 10 || recency > 0.5f)
                    {
                        evidence.Add(new DimensionEvidence(
                            signal, layer, obs.Formula,
                            MathF.Round(level, 2), MathF.Round(recency, 3)));
                    }
                }

                if (weightTotal > 0)
                {
                    var avgLevel = weightedSum / weightTotal;
                    signalLevels.Add((avgLevel, affinity, sorted.Count));
                }
            }

            // Compute dimension score from weighted signal levels
            var totalWeight = signalLevels.Sum(s => s.Weight);
            var rawScore = totalWeight > 0
                ? signalLevels.Sum(s => s.Level * s.Weight) / totalWeight
                : 3f; // neutral default

            // Map 1-5 level to 0-100 score
            var score = (int)Math.Clamp((rawScore - 1f) / 4f * 100f, 0, 100);

            // Confidence: sigmoid of total evidence count
            var totalEvidence = relevantObs.Sum(g => g.Count());
            var confidence = LevelEstimator.Sigmoid(totalEvidence, 5f);

            // Consistency: inverse of variance across signal levels
            var consistency = allLevels.Count > 1
                ? 1f - Math.Clamp(Variance(allLevels) / 4f, 0f, 1f) // normalize variance
                : 0f;

            // Trim evidence to top entries
            var topEvidence = evidence
                .OrderByDescending(e => e.Recency)
                .Take(5)
                .ToList();

            scores.Add(new DimensionScore(
                dim.Name, dim.Section, dim.Category,
                score, MathF.Round(confidence, 3), MathF.Round(consistency, 3),
                totalEvidence, topEvidence));
        }

        return scores;
    }

    private static float Variance(List<float> values)
    {
        if (values.Count < 2) return 0f;
        var mean = values.Average();
        return values.Select(v => (v - mean) * (v - mean)).Sum() / values.Count;
    }
}
