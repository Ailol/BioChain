namespace BioChain.AnalysisFramework;

public static class ResistanceEngine
{
    public static float HalvingWeight(int index) => MathF.Pow(0.5f, index);

    public static float ProfileMaturity(int totalObservations, float avgVariance)
    {
        var obsScore = 1f - MathF.Exp(-totalObservations / 50f);
        var varPenalty = Math.Clamp(avgVariance * 2f, 0f, 0.5f);
        return Math.Clamp(obsScore - varPenalty, 0f, 1f);
    }
}
