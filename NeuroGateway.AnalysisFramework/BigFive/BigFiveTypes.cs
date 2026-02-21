namespace NeuroGateway.AnalysisFramework.BigFive;

// Big Five (OCEAN) trait score: 0-100 scale where 50 = population average
public sealed record BigFiveTraitScore(
    string Trait,       // "openness", "conscientiousness", "extraversion", "agreeableness", "neuroticism"
    string Label,       // "Openness to Experience"
    float Score,        // 0.0 - 1.0 (0.5 = population center)
    float HighSim,      // raw similarity to high-pole prototype
    float LowSim        // raw similarity to low-pole prototype
);

public sealed record BigFiveResult(
    List<BigFiveTraitScore> Traits,
    string Note
);
