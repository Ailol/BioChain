namespace NeuroGateway.Models;

public sealed record ChemicalLevelDto(
    string Chemical,
    string Layer,
    float Level,
    int ObservationCount,
    float Variance
);

public sealed record ChemicalProfileDto(
    string Person,
    float Maturity,
    int TotalObservations,
    int UniqueChemicals,
    List<ChemicalLevelDto> Levels,
    List<ChemicalLevelDto> TopFive
);

public sealed record ChemicalForecastDto(
    string Chemical,
    string Trend,
    float CurrentLevel,
    float ProjectedLevel,
    float Velocity,
    bool ApproachingOptimal,
    bool DriftingFromOptimal,
    string? RiskNote
);

public sealed record CascadeAlertDto(
    string TriggerChemical,
    List<string> AffectedChemicals,
    string Mechanism,
    string Severity
);

public sealed record PersonalForecastDto(
    List<ChemicalForecastDto> Chemicals,
    List<CascadeAlertDto> ActiveCascades,
    List<string> StableFoundation,
    List<string> InFlux,
    string OverallTrajectory,
    string Narrative
);

public sealed record PrescriptionDto(
    string Modality,
    string Rationale,
    List<string> TargetChemicals,
    float Priority
);

public sealed record HealthIndicatorsDto(
    bool BurnoutRisk,
    float? BurnoutRatio,
    string? BurnoutNote,
    bool GrowthWindowOpen,
    string? GrowthNote,
    string? OvertrainingIndicator,
    string? OvertrainingRecommendation
);

public sealed record TrajectoryPointDto(DateTime Date, float Level);

public sealed record ChemicalTrajectoryDto(
    string Chemical,
    string Layer,
    List<TrajectoryPointDto> Points
);

public sealed record TrajectoryResultDto(
    string Person,
    int PeriodDays,
    List<ChemicalTrajectoryDto> Chemicals
);

public sealed record CheckInResponse(bool AnalysisTriggered, int WordCount, string? Status);

public sealed record DashboardResultDto(
    ChemicalProfileDto Profile,
    PersonalForecastDto Forecast,
    List<PrescriptionDto> Prescriptions,
    HealthIndicatorsDto Health
);

// ── Key Chemicals (computed, display-ready) ──

public sealed record KeyChemicalDto(
    string Chemical,
    string Label,
    string Layer,
    string LayerColor,
    float Level,
    string LevelLabel,
    float OptimalCenter,
    float OptimalLow,
    float OptimalHigh,
    string Significance,
    string SignificanceIcon,
    float Importance,
    int ObservationCount
);

public sealed record KeyChemicalsResultDto(
    string Person,
    List<KeyChemicalDto> Chemicals,
    string Narrative
);

// ── Strengths & Challenges (AI-generated, display-ready) ──

public sealed record StrengthChallengeItemDto(
    string Type,
    string Indicator,
    string Title,
    string ChemicalKey,
    string Label,
    string Layer,
    string LayerColor,
    float Level,
    float OptimalCenter,
    float Deviation,
    string LevelLabel,
    string Explanation,
    string PracticalAdvice,
    string BrainExercise,
    string[] RelatedChemicals,
    string[] RelatedLabels
);

public sealed record StrengthsChallengesResultDto(
    string Person,
    List<StrengthChallengeItemDto> Strengths,
    List<StrengthChallengeItemDto> Challenges,
    string Summary,
    string GeneratedAt
);

// ── Cross-Profile: Strength x Challenge interactions with LLM-generated suggestions ──

public sealed record CrossProfileItemDto(
    string StrengthChemical,
    string StrengthLabel,
    string ChallengeChemical,
    string ChallengeLabel,
    float Similarity,
    string Affects,
    string Interaction,
    string Suggestion,
    string Mechanism
);

public sealed record CrossProfileResultDto(
    string Person,
    List<CrossProfileItemDto> Interactions,
    string Narrative,
    string GeneratedAt
);

// ── Big Five / OCEAN (embedding-based classification) ──

public sealed record BigFiveTraitScoreDto(
    string Trait,
    string Label,
    float Score,
    float HighSim,
    float LowSim
);

public sealed record BigFiveResultDto(
    string Person,
    List<BigFiveTraitScoreDto> Traits,
    string Note
);

// ── MBTI (embedding-based classification) ──

public sealed record MbtiTypeScoreDto(
    string TypeCode,
    string TypeLabel,
    float Similarity
);

public sealed record MbtiResultDto(
    string Person,
    string TypeCode,
    string TypeLabel,
    List<MbtiTypeScoreDto> RankedTypes,
    string Note
);

// ── Personality Narrative (AI-generated, MBTI + Big Five + biochemistry) ──

public sealed record TraitDriverDto(
    string Trait,
    string Label,
    float Score,
    string Narrative,
    string Pattern,
    string[] KeyChemicals
);

public sealed record MbtiInsightDto(
    string CognitiveStack,
    string StrengthsNarrative,
    string BlindSpots,
    string GrowthPath,
    string[] DominantChemicals
);

public sealed record PersonalityNarrativeDto(
    string Person,
    string MbtiSummary,
    string BigFiveSummary,
    string TypeChemistry,
    string OverallPattern,
    MbtiInsightDto? MbtiInsight,
    List<TraitDriverDto> TraitDrivers,
    string GeneratedAt
);
