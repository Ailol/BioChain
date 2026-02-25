namespace BioChain.AnalysisFramework.Mbti;

public sealed record MbtiTypeScore(
    string TypeCode,
    string TypeLabel,
    float Similarity
);

public sealed record MbtiEmbeddingResult(
    string TypeCode,
    string TypeLabel,
    List<MbtiTypeScore> RankedTypes,
    string Note
);
