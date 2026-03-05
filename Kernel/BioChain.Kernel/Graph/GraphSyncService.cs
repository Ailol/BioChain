using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BioChain.Kernel.Graph;

/// <summary>
/// Automatically syncs the PostgreSQL graph (BioChain v5.1 append-only)
/// into Neo4j via LISTEN/NOTIFY + export_graph_json().
///
/// Architecture:
///   PG INSERT -> notify_graph_insert trigger -> graph_changed channel
///     -> this service (LISTEN) -> debounce per subject_id
///     -> refresh_graph() -> export_graph_json(subject_id)
///     -> Neo4j DELETE + CREATE (full replace per subject)
/// </summary>
public sealed class GraphSyncService : BackgroundService
{
    private readonly IGraphStore _graphStore;
    private readonly string _pgConnString;
    private readonly int _debounceMs;
    private readonly ILogger<GraphSyncService> _logger;

    // Debounce: subject_id -> CTS that fires SyncPersonAsync after quiet period
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pending = new();

    // Skip guard: subject_id -> SHA256 hash of last-synced JSON
    private readonly ConcurrentDictionary<Guid, string> _lastHash = new();

    // Health metrics
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSyncTime = new();
    private long _totalSyncs;
    private long _failedSyncs;
    private string? _lastError;

    public IReadOnlyDictionary<Guid, DateTime> LastSyncTimes => _lastSyncTime;
    public int QueueDepth => _pending.Count;
    public long TotalSyncsCompleted => Interlocked.Read(ref _totalSyncs);
    public long FailedSyncCount => Interlocked.Read(ref _failedSyncs);
    public string? LastError => _lastError;

    public GraphSyncService(
        IGraphStore graphStore,
        IConfiguration config,
        ILogger<GraphSyncService> logger)
    {
        _graphStore = graphStore;
        _pgConnString = config.GetConnectionString("biochain")
            ?? throw new InvalidOperationException("ConnectionStrings:biochain is required");
        _debounceMs = int.TryParse(config["Neo4j:DebounceMs"], out var d) ? d : 500;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[GraphSync] Starting — debounce={DebounceMs}ms", _debounceMs);

        // Phase A: startup full sync
        await StartupFullSyncAsync(stoppingToken);

        // Phase B: LISTEN loop with connection resilience
        await ListenLoopAsync(stoppingToken);
    }

    // -- Phase A: Startup Full Sync --

    private async Task StartupFullSyncAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_pgConnString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand("SELECT id FROM entity", conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var subjectIds = new List<Guid>();
            while (await reader.ReadAsync(ct))
                subjectIds.Add(reader.GetGuid(0));
            await reader.CloseAsync();

            foreach (var pid in subjectIds)
                await SyncPersonAsync(pid, conn, ct);

            _logger.LogInformation("[GraphSync] Startup sync complete: {Count} subjects", subjectIds.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[GraphSync] Startup sync failed — will rely on LISTEN for incremental sync");
        }
    }

    // -- Phase B: LISTEN Loop --

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        var delay = 1;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_pgConnString);
                await conn.OpenAsync(ct);
                conn.Notification += OnNotification;

                await using var cmd = new NpgsqlCommand("LISTEN graph_changed", conn);
                await cmd.ExecuteNonQueryAsync(ct);
                delay = 1; // reset on successful connection

                _logger.LogInformation("[GraphSync] LISTEN connected");

                while (!ct.IsCancellationRequested)
                    await conn.WaitAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[GraphSync] LISTEN connection lost, reconnecting in {Delay}s", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                delay = Math.Min(delay * 2, 30);
            }
        }
    }

    // -- Phase C: Notification -> Debounce --

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.Payload);
            var subjectIdStr = doc.RootElement.GetProperty("entity_id").GetString();
            if (!Guid.TryParse(subjectIdStr, out var subjectId)) return;

            ScheduleSync(subjectId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GraphSync] Failed to parse notification payload: {Payload}", e.Payload);
        }
    }

    private void ScheduleSync(Guid subjectId)
    {
        // Cancel any existing debounce timer for this subject
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

                // Debounce window expired — sync this subject
                _pending.TryRemove(subjectId, out _);

                await using var conn = new NpgsqlConnection(_pgConnString);
                await conn.OpenAsync();
                await SyncPersonAsync(subjectId, conn, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Debounce reset — another notification arrived, this is expected
            }
            catch (Exception ex)
            {
                _pending.TryRemove(subjectId, out _);
                Interlocked.Increment(ref _failedSyncs);
                _lastError = $"{DateTime.UtcNow:s} subject={subjectId}: {ex.Message}";
                _logger.LogError(ex, "[GraphSync] Sync failed for subject {SubjectId}", subjectId);
            }
        });
    }

    // -- Sync: PG export -> Neo4j DELETE + CREATE --

    private async Task SyncPersonAsync(Guid subjectId, NpgsqlConnection conn, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Refresh materialized view
        await using (var cmd = new NpgsqlCommand("SELECT refresh_graph()", conn))
            await cmd.ExecuteNonQueryAsync(ct);

        // 2. Export graph JSON
        await using var exportCmd = new NpgsqlCommand("SELECT export_graph_json($1)", conn);
        exportCmd.Parameters.AddWithValue(subjectId);
        var jsonObj = await exportCmd.ExecuteScalarAsync(ct);
        var json = jsonObj?.ToString();

        if (string.IsNullOrEmpty(json))
        {
            _logger.LogDebug("[GraphSync] No graph data for subject {SubjectId}", subjectId);
            return;
        }

        // 3. Skip guard — hash comparison
        var hash = ComputeHash(json);
        if (_lastHash.TryGetValue(subjectId, out var prev) && prev == hash)
        {
            _logger.LogDebug("[GraphSync] Skipped subject {SubjectId} (unchanged)", subjectId);
            return;
        }

        // 4. Delegate to graph store
        await _graphStore.SyncPersonAsync(subjectId, json, ct);

        // 5. Update skip guard + metrics
        _lastHash[subjectId] = hash;
        _lastSyncTime[subjectId] = DateTime.UtcNow;
        Interlocked.Increment(ref _totalSyncs);

        sw.Stop();
        _logger.LogInformation(
            "[GraphSync] Synced subject {SubjectId} in {Ms}ms",
            subjectId, sw.ElapsedMilliseconds);
    }

    private static string ComputeHash(string json)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public override void Dispose()
    {
        foreach (var cts in _pending.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _pending.Clear();
        base.Dispose();
    }
}
