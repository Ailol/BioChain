namespace BioChain.Repository.Repositories;

/// <summary>
/// Wraps raw PostgreSQL function calls for gate evaluation and graph serialisation.
/// </summary>
public interface IGraphQueryRepository
{
    /// <summary>
    /// Calls PG <c>evaluate_gate($gateId, $subjectId)</c> → bool.
    /// </summary>
    Task<bool> EvaluateGateAsync(int gateId, Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Calls PG <c>refresh_graph()</c> then <c>serialize_profile_dsl($subjectId)</c> → compact DSL string.
    /// </summary>
    Task<string> SerializeProfileDslAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Calls PG <c>export_graph_json($subjectId)</c> → full graph as JSONB string.
    /// </summary>
    Task<string> ExportGraphJsonAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Calls PG <c>find_feedback_loops($subjectId, true)</c> → detected feedback cycles.
    /// </summary>
    Task<List<FeedbackLoopRow>> FindFeedbackLoopsAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Calls PG <c>find_dysreg_cascades($subjectId, 5, true)</c> → dysregulation cascade paths.
    /// </summary>
    Task<List<DysregCascadeRow>> FindDysregCascadesAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Queries <c>v_region_activity</c> for computed region health data.
    /// </summary>
    Task<List<RegionActivityRow>> GetRegionActivityAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Queries BIND entries from the analysis table for a subject.
    /// Returns behavioral/functional composites defined over real neurochemical signals.
    /// </summary>
    Task<List<BindEntryRow>> GetBindEntriesAsync(Guid subjectId, CancellationToken ct = default);
}

// ── Row DTOs for set-returning functions ──

public record FeedbackLoopRow(string[] LoopPath, string[] Operators, bool IsPositive);

public record DysregCascadeRow(string RootCode, string DysregType, int CascadeDepth, string[] AffectedPath);

public record RegionActivityRow(
    Guid EntityId, int RegionId, string Code, string FullName, string System,
    string ActivityState,
    long SignalCount, long Elevated, long Depleted,
    long ReceptorCount, long ReceptorsImpaired, long DysregCount);

public record BindEntryRow(int AnalysisId, string Formula, string? Status);
