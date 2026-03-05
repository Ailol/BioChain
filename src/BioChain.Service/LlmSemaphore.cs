namespace BioChain.Service;

/// <summary>
/// Limits concurrent LLM requests to prevent overwhelming local inference servers
/// (e.g. LM Studio with a single large model). Registered as Singleton.
/// </summary>
public sealed class LlmSemaphore(int maxConcurrency = 1) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(maxConcurrency, maxConcurrency);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
