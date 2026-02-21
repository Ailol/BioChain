namespace NeuroGateway.AnalysisFramework;

public static class DimensionDefinitions
{
    public enum ScoringMode
    {
        Work,
        Private,
    }

    public sealed record DimensionDef(
        string Name,
        string Section,
        string Category,
        string Description,
        IReadOnlyDictionary<string, float> ChemicalAffinity,
        float WorkRelevance = 1.0f,
        float PrivateRelevance = 1.0f,
        string? ArchetypeName = null,
        string? ArchetypeEssence = null
    )
    {
        public float GetModeMultiplier(ScoringMode mode) =>
            mode switch
            {
                ScoringMode.Work => WorkRelevance,
                ScoringMode.Private => PrivateRelevance,
                _ => 1.0f,
            };
    }
}
