using BioChain.AnalysisFramework.OLD;

namespace BioChain.AnalysisFramework.BigFive;

// Big Five (OCEAN) classification via per-chemical embedding comparison.
// For each trait, compares person's observation embeddings against HIGH-pole
// and LOW-pole prototype embeddings, computes score = highSim / (highSim + lowSim),
// then applies power-curve contrast expansion to amplify small deviations from center.
public static class BigFiveClassifier
{
    public static readonly (string Key, string Label)[] Traits =
    [
        ("openness", "Openness to Experience"),
        ("conscientiousness", "Conscientiousness"),
        ("extraversion", "Extraversion"),
        ("agreeableness", "Agreeableness"),
        ("neuroticism", "Neuroticism"),
    ];

    // Gamma for power-curve contrast expansion. Lower = more spread from center.
    private const float ContrastGamma = 0.4f;

    // Classify using per-chemical embedding comparison for all 5 traits.
    // personChemVectors: chemical → mean-pooled observation embedding
    // prototypes: poleKey (e.g. "openness_high") → chemical → prototype embedding
    public static BigFiveResult Classify(
        Dictionary<string, float[]> personChemVectors,
        Dictionary<string, Dictionary<string, float[]>> prototypes)
    {
        if (personChemVectors.Count == 0)
            return new BigFiveResult([],
                "No observation data available for Big Five classification.");

        var results = new List<BigFiveTraitScore>();

        foreach (var (traitKey, label) in Traits)
        {
            var highPole = $"{traitKey}_high";
            var lowPole = $"{traitKey}_low";

            float highSim = SumSimilarity(personChemVectors, prototypes, highPole);
            float lowSim = SumSimilarity(personChemVectors, prototypes, lowPole);
            float total = highSim + lowSim;
            float rawRatio = total > 0 ? highSim / total : 0.5f;
            float score = ContrastExpand(rawRatio);

            results.Add(new BigFiveTraitScore(traitKey, label, score, highSim, lowSim));
        }

        int chemCount = personChemVectors.Count;
        return new BigFiveResult(results,
            $"Classified from {chemCount} chemical observation vectors against differentiated pole prototypes.");
    }

    // Power-curve contrast: expand deviations from center
    private static float ContrastExpand(float rawRatio)
    {
        float deviation = rawRatio - 0.5f;
        float sign = deviation >= 0 ? 1f : -1f;
        float magnitude = MathF.Abs(2f * deviation);
        float expanded = MathF.Pow(magnitude, ContrastGamma);
        return Math.Clamp(0.5f + sign * 0.5f * expanded, 0f, 1f);
    }

    private static float SumSimilarity(
        Dictionary<string, float[]> personChemVectors,
        Dictionary<string, Dictionary<string, float[]>> prototypes,
        string poleKey)
    {
        if (!prototypes.TryGetValue(poleKey, out var chemEmbeddings))
            return 0f;

        float total = 0f;
        foreach (var (chemical, personVec) in personChemVectors)
        {
            if (!chemEmbeddings.TryGetValue(chemical, out var protoVec)) continue;
            total += EmbeddingMath.CosineSimilarity(personVec, protoVec);
        }
        return total;
    }
}
