namespace BioChain.AnalysisFramework.OLD;

// Bitcoin-style difficulty function: profiles harden over time.
// More observations + closer to optimal + more consistent = harder to shift.
public static class ResistanceEngine
{
    private const int HalvingEpochSize = 10;
    private const float DefaultMassWeight = 1.0f;
    private const float VarianceFloor = 0.1f;

    // Proximity resistance: chemicals near their optimal center are harder to shift.
    // Range: 1.0 (far from optimal, easy to move) → 10.0 (at optimal, very hard to move)
    public static float ProximityResistance(
        float currentLevel,
        float optimalCenter,
        float maxDistance = 0.5f
    )
    {
        var dist = MathF.Abs(currentLevel - optimalCenter);
        var proximity = 1.0f - MathF.Min(dist / maxDistance, 1.0f);
        return 1.0f + 9.0f * MathF.Pow(proximity, 3);
    }

    // Observation mass: more evidence anchors the profile.
    // result = 1.0 + log2(count + 1) * massWeight
    public static float MassResistance(int observationCount, float massWeight = DefaultMassWeight)
    {
        return 1.0f + MathF.Log2(observationCount + 1) * massWeight;
    }

    // Halving schedule: observation weight decays by epoch.
    // Newest 10 observations = full weight, next 10 = 0.5, next 10 = 0.25, etc.
    // index 0 = newest observation.
    public static float HalvingWeight(int observationIndex, float baseWeight = 1.0f)
    {
        var epoch = observationIndex / HalvingEpochSize;
        return baseWeight / MathF.Pow(2, epoch);
    }

    // Variance modulation: inconsistent data keeps profile malleable.
    // High variance → low factor → lower difficulty → easier to shift.
    // Low variance → high factor → higher difficulty → harder to shift.
    public static float ConsistencyFactor(float variance)
    {
        return 1.0f / (variance + VarianceFloor);
    }

    // Combined difficulty: proximity * mass * consistency
    public static float ComputeDifficulty(
        float currentLevel,
        float optimalCenter,
        int obsCount,
        float variance
    )
    {
        var prox = ProximityResistance(currentLevel, optimalCenter);
        var mass = MassResistance(obsCount);
        var cons = ConsistencyFactor(variance);
        return prox * mass * cons;
    }

    // Given a raw shift, return the difficulty-adjusted shift.
    // adjustedShift = rawShift / difficulty
    public static float ApplyResistance(
        float currentLevel,
        float optimalCenter,
        int obsCount,
        float variance,
        float rawShift
    )
    {
        var difficulty = ComputeDifficulty(currentLevel, optimalCenter, obsCount, variance);
        return rawShift / difficulty;
    }

    // Profile maturity: 0..1 (how hardened is this profile overall?)
    // Sigmoid of total observations scaled by average variance.
    // High obs + low variance → near 1.0 (hardened).
    // Few obs + high variance → near 0.0 (malleable).
    public static float ProfileMaturity(int totalObservations, float avgVariance)
    {
        var consistencyBoost = ConsistencyFactor(avgVariance);
        var raw = totalObservations * consistencyBoost;
        // Sigmoid centered at 50 effective observations
        return 1.0f / (1.0f + MathF.Exp(-(raw - 50f) / 15f));
    }
}
