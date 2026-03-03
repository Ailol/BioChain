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
}
