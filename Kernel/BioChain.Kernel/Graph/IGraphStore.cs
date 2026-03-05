namespace BioChain.Kernel.Graph;

/// <summary>
/// Abstraction over graph database operations.
/// PG is source of truth; graph store is a read-optimized secondary.
/// </summary>
public interface IGraphStore
{
    /// <summary>
    /// Replace all graph data for a subject with the given JSON
    /// (output of PG export_graph_json function).
    /// </summary>
    Task SyncPersonAsync(Guid subjectId, string graphJson, CancellationToken ct = default);

    /// <summary>
    /// Remove all graph data for a subject.
    /// </summary>
    Task DeletePersonAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Verify the graph store is reachable.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
