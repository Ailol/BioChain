using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BioChain.Repository.Listeners;

/// <summary>
/// PostgreSQL LISTEN/NOTIFY listener with per-subject debounce.
/// Extracted from AgentEcosystemService (LISTEN loop + debounce logic).
/// </summary>
public sealed class PostgresGraphChangeListener : IGraphChangeListener
{
    private readonly string _connString;
    private readonly int _debounceMs;
    private readonly ILogger<PostgresGraphChangeListener> _logger;

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pending = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<string>> _changedSignals = new();

    private GraphChangeHandler? _handler;

    public PostgresGraphChangeListener(IConfiguration config, ILogger<PostgresGraphChangeListener> logger)
    {
        _connString = config.GetConnectionString("biochain")
            ?? throw new InvalidOperationException("ConnectionStrings:biochain is required");
        _debounceMs = int.TryParse(config["AgentEcosystem:DebounceMs"], out var d) ? d : 2000;
        _logger = logger;
    }

    public async Task ListenAsync(GraphChangeHandler handler, CancellationToken ct)
    {
        _handler = handler;
        _logger.LogInformation("[GraphChangeListener] Starting — debounce={DebounceMs}ms", _debounceMs);

        var delay = 1;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync(ct);
                conn.Notification += OnNotification;

                await using var cmd = new NpgsqlCommand("LISTEN graph_changed", conn);
                await cmd.ExecuteNonQueryAsync(ct);
                delay = 1;

                _logger.LogInformation("[GraphChangeListener] LISTEN connected");

                while (!ct.IsCancellationRequested)
                    await conn.WaitAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[GraphChangeListener] Connection lost, reconnecting in {Delay}s", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                delay = Math.Min(delay * 2, 30);
            }
        }
    }

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.Payload);
            var root = doc.RootElement;

            var subjectIdStr = root.GetProperty("entity_id").GetString();
            if (!Guid.TryParse(subjectIdStr, out var subjectId)) return;

            if (root.TryGetProperty("code", out var c) && c.GetString() is { } code)
            {
                var bag = _changedSignals.GetOrAdd(subjectId, _ => []);
                bag.Add(code);
            }

            ScheduleCallback(subjectId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GraphChangeListener] Failed to parse notification: {Payload}", e.Payload);
        }
    }

    private void ScheduleCallback(Guid subjectId)
    {
        if (_pending.TryRemove(subjectId, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _pending[subjectId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceMs, cts.Token);
                _pending.TryRemove(subjectId, out _);

                var changedCodes = Array.Empty<string>();
                if (_changedSignals.TryRemove(subjectId, out var bag))
                    changedCodes = bag.Distinct().ToArray();

                if (_handler is not null)
                    await _handler(subjectId, changedCodes);
            }
            catch (OperationCanceledException)
            {
                // Debounce reset — expected
            }
            catch (Exception ex)
            {
                _pending.TryRemove(subjectId, out _);
                _logger.LogError(ex, "[GraphChangeListener] Handler failed for subject {SubjectId}", subjectId);
            }
        });
    }

    public void Dispose()
    {
        foreach (var cts in _pending.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _pending.Clear();
    }
}
