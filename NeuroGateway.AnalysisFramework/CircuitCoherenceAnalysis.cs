using NeuroGateway.Models;

namespace NeuroGateway.AnalysisFramework;

// Chemical interaction graph analysis for a dimension.
// Blends level-based agreement, embedding cosine similarity,
// and known biochemical interactions into pairwise edge weights.
public static class CircuitCoherenceAnalysis
{
    private const float EmbeddingCorrelationWeight = 0.4f; // 60% level + 40% embedding
    private const float KnownInteractionBlendWeight = 0.3f; // 30% known prior, 70% observed

    public static CircuitCoherence? Compute(
        Dictionary<string, List<float>> chemicalLevelMap,
        Dictionary<string, List<float[]>> chemicalEmbeddingMap,
        IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)> interactions)
    {
        var chemicals = chemicalLevelMap.Keys.ToList();
        if (chemicals.Count < 2) return null;

        // Mean level per chemical
        var means = chemicals.ToDictionary(
            c => c, c => chemicalLevelMap[c].Average(),
            StringComparer.OrdinalIgnoreCase);

        // Mean-pooled embedding centroid per chemical
        var centroids = new Dictionary<string, float[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var chem in chemicals)
        {
            centroids[chem] = chemicalEmbeddingMap.TryGetValue(chem, out var embs) && embs.Count > 0
                ? EmbeddingMath.MeanPool(embs) : null;
        }

        var edges = new List<ChemicalEdge>();
        var coherenceValues = new List<float>();

        for (var i = 0; i < chemicals.Count; i++)
        {
            for (var j = i + 1; j < chemicals.Count; j++)
            {
                var a = chemicals[i];
                var b = chemicals[j];

                // Level-based correlation: mean-level agreement
                var levelDiff = MathF.Abs(means[a] - means[b]);
                var levelCorrelation = 1f - levelDiff / 4f;

                // Embedding-based correlation
                float observedCorrelation;
                if (centroids[a] is not null && centroids[b] is not null)
                {
                    var embeddingCorrelation = EmbeddingMath.CosineSimilarity(centroids[a]!, centroids[b]!);
                    observedCorrelation = (1f - EmbeddingCorrelationWeight) * levelCorrelation
                                         + EmbeddingCorrelationWeight * embeddingCorrelation;
                }
                else
                {
                    observedCorrelation = levelCorrelation;
                }

                // Check for known interaction (either direction)
                var aKey = a.ToLowerInvariant();
                var bKey = b.ToLowerInvariant();
                float? knownModFactor = null;
                string? knownMechanism = null;

                if (interactions.TryGetValue((aKey, bKey), out var modAB))
                {
                    knownModFactor = modAB.ModFactor;
                    knownMechanism = modAB.Mechanism;
                }
                else if (interactions.TryGetValue((bKey, aKey), out var modBA))
                {
                    knownModFactor = modBA.ModFactor;
                    knownMechanism = modBA.Mechanism;
                }

                // Blend observed with known interaction prior
                float blendedCorrelation;
                if (knownModFactor.HasValue)
                {
                    var knownCorrelation = Math.Clamp(knownModFactor.Value, -1f, 1f);
                    blendedCorrelation = (1f - KnownInteractionBlendWeight) * observedCorrelation
                                       + KnownInteractionBlendWeight * knownCorrelation;
                }
                else
                {
                    blendedCorrelation = observedCorrelation;
                }

                coherenceValues.Add(blendedCorrelation);

                var relationship = blendedCorrelation > 0.7f ? "Synergistic"
                    : blendedCorrelation > 0.3f ? "Cooperative"
                    : blendedCorrelation > -0.3f ? "Independent"
                    : blendedCorrelation > -0.7f ? "Opposing"
                    : "Antagonistic";

                edges.Add(new ChemicalEdge(a, b,
                    MathF.Round(blendedCorrelation, 3), relationship,
                    knownModFactor.HasValue ? MathF.Round(knownModFactor.Value, 3) : null,
                    knownMechanism));
            }
        }

        var coherenceScore = coherenceValues.Count > 0
            ? Math.Clamp(coherenceValues.Average(), 0f, 1f) : 0f;

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
