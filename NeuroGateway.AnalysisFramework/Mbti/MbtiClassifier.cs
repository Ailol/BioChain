namespace NeuroGateway.AnalysisFramework.Mbti;

// Pure classification logic: given pre-loaded embeddings, compute MBTI type.
// No I/O, no DB, no embedding generation — just math on float[] vectors.
public static class MbtiClassifier
{
    // Classify a person's MBTI type from their per-chemical observation embeddings
    // compared against per-chemical prototype embeddings for all 16 types.
    // personChemVectors: chemical → mean-pooled observation embedding
    // prototypes: typeCode → chemical → prototype embedding
    public static MbtiEmbeddingResult Classify(
        Dictionary<string, float[]> personChemVectors,
        Dictionary<string, Dictionary<string, float[]>> prototypes)
    {
        if (personChemVectors.Count == 0)
            return new MbtiEmbeddingResult("????", "Undefined", [],
                "No valid embeddings found for classification.");

        // Sum cosine similarities across all matched chemicals per type.
        // Types that explain more of the person's observed chemicals with high
        // similarity accumulate more total evidence — pure embedding signal.
        int totalPersonChems = personChemVectors.Count;
        var typeScores = new Dictionary<string, (float TotalSim, int MatchCount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (typeCode, chemEmbeddings) in prototypes)
        {
            float totalSim = 0f;
            int matchCount = 0;

            foreach (var (chemical, personVec) in personChemVectors)
            {
                if (!chemEmbeddings.TryGetValue(chemical, out var protoVec)) continue;

                totalSim += EmbeddingMath.CosineSimilarity(personVec, protoVec);
                matchCount++;
            }

            if (matchCount > 0)
                typeScores[typeCode] = (totalSim, matchCount);
        }

        // Rank by total accumulated similarity
        var ranked = typeScores
            .Select(kvp => new MbtiTypeScore(
                kvp.Key,
                MbtiPrototypes.TypeLabels.GetValueOrDefault(kvp.Key, "Unknown"),
                kvp.Value.TotalSim))
            .OrderByDescending(t => t.Similarity)
            .ToList();

        if (ranked.Count == 0)
            return new MbtiEmbeddingResult("????", "Undefined", [],
                "No chemical overlap between observations and prototypes.");

        var top = ranked[0];
        var runner = ranked.Count > 1 ? ranked[1] : null;
        var gap = runner is not null ? top.Similarity - runner.Similarity : 1f;
        var topMatch = typeScores.TryGetValue(top.TypeCode, out var ti) ? ti.MatchCount : 0;

        var note = gap < 0.05f
            ? $"Very close call between {top.TypeCode} and {runner?.TypeCode} (gap={gap:F3}, {topMatch}/{totalPersonChems} chemicals matched)"
            : gap < 0.15f
                ? $"Leaning {top.TypeCode} over {runner?.TypeCode} (gap={gap:F3}, {topMatch}/{totalPersonChems} chemicals matched)"
                : $"Clear {top.TypeCode} (gap={gap:F3} over {runner?.TypeCode}, {topMatch}/{totalPersonChems} chemicals matched)";

        return new MbtiEmbeddingResult(top.TypeCode, top.TypeLabel, ranked, note);
    }

    // Mean-pool a person's observation embeddings grouped by chemical
    public static Dictionary<string, float[]> BuildPersonChemVectors(
        IReadOnlyList<(string Chemical, float[] Embedding)> observations)
    {
        var result = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        var grouped = observations.GroupBy(e => e.Chemical, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var embeddings = group.Select(e => e.Embedding).ToList();
            var pooled = EmbeddingMath.MeanPool(embeddings);
            if (pooled is not null)
                result[group.Key] = pooled;
        }

        return result;
    }
}
