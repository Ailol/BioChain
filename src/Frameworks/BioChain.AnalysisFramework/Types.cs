namespace BioChain.AnalysisFramework;

public sealed record DimensionDef(
    string Name, string Section, string Category, string Description,
    IReadOnlyDictionary<string, float> SignalAffinity,
    float WorkRelevance, float PrivateRelevance,
    string? ArchetypeName, string? ArchetypeEssence);

public sealed record OptimalRange(float Center, float Low, float High);

public sealed record Prescription(string Modality, string Rationale, List<string> TargetSignals, float Priority);

public sealed record OvertrainingResult(string Indicator, string Recommendation);

public enum ForecastTrend { Rising, Stable, Falling }

public sealed record SignalForecast(
    string Signal, ForecastTrend Trend, float CurrentLevel, float ProjectedLevel,
    float Velocity, bool ApproachingOptimal, bool DriftingFromOptimal, string? RiskNote);

public sealed record CascadeAlert(
    string TriggerSignal, List<string> AffectedSignals, string Mechanism, string Severity);

public sealed record PersonalForecast(
    List<SignalForecast> Signals, List<CascadeAlert> ActiveCascades,
    List<string> StableFoundation, List<string> InFlux,
    string OverallTrajectory, string Narrative);

public enum ScoringMode { Work, Private }
