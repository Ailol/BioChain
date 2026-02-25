namespace NeuroGateway.Models;

public sealed record SignalLevelDto(
    string Signal,
    string Layer,
    float Level,
    int ObservationCount,
    float Variance
);

public sealed record SignalProfileDto(
    string Person,
    float Maturity,
    int TotalObservations,
    int UniqueSignals,
    List<SignalLevelDto> Levels,
    List<SignalLevelDto> TopFive
);

public sealed record SignalForecastDto(
    string Signal,
    string Trend,
    float CurrentLevel,
    float ProjectedLevel,
    float Velocity,
    bool ApproachingOptimal,
    bool DriftingFromOptimal,
    string? RiskNote
);

public sealed record CascadeAlertDto(
    string TriggerSignal,
    List<string> AffectedSignals,
    string Mechanism,
    string Severity
);

public sealed record PersonalForecastDto(
    List<SignalForecastDto> Signals,
    List<CascadeAlertDto> ActiveCascades,
    List<string> StableFoundation,
    List<string> InFlux,
    string OverallTrajectory,
    string Narrative
);

public sealed record PrescriptionDto(
    string Modality,
    string Rationale,
    List<string> TargetSignals,
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

public sealed record SignalTrajectoryDto(
    string Signal,
    string Layer,
    List<TrajectoryPointDto> Points
);

public sealed record TrajectoryResultDto(
    string Person,
    int PeriodDays,
    List<SignalTrajectoryDto> Signals
);

public sealed record CheckInResponse(bool AnalysisTriggered, int WordCount, string? Status);

public sealed record DashboardResultDto(
    SignalProfileDto Profile,
    PersonalForecastDto Forecast,
    List<PrescriptionDto> Prescriptions,
    HealthIndicatorsDto Health
);

// ── Key Signals (computed, display-ready) ──

public sealed record KeySignalDto(
    string Signal,
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

public sealed record KeySignalsResultDto(
    string Person,
    List<KeySignalDto> Signals,
    string Narrative
);

// ── Strengths & Challenges (AI-generated, display-ready) ──

public sealed record StrengthChallengeItemDto(
    string Type,
    string Indicator,
    string Title,
    string SignalKey,
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
    string[] RelatedSignals,
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
    string StrengthSignal,
    string StrengthLabel,
    string ChallengeSignal,
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

