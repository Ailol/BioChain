namespace BioChain.Repository.Listeners;

/// <summary>
/// Callback invoked after the debounce window expires for a subject.
/// </summary>
public delegate Task GraphChangeHandler(Guid subjectId, string[] changedCodes);

/// <summary>
/// Listens for PG NOTIFY on <c>graph_changed</c>, debounces by subject_id,
/// and fires <see cref="GraphChangeHandler"/> when the debounce window expires.
/// </summary>
public interface IGraphChangeListener : IDisposable
{
    Task ListenAsync(GraphChangeHandler handler, CancellationToken ct);
}
