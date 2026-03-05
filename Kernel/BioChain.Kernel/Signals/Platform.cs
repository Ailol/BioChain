using Marten;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orleans;
using Wolverine;
using BioChain.Kernel.Graph;

namespace BioChain.Kernel.Signals;

// ──────────────────────── ORLEANS GRAIN ────────────────────────

public interface IWorldGrain : IGrainWithGuidKey
{
    ValueTask<TickResult> InjectAsync(Input input);
    ValueTask<TickResult> TickAsync();
    ValueTask StartAsync(string connectionString);
    ValueTask StopAsync();
}

public sealed class WorldGrain : Grain, IWorldGrain
{
    private TickCtx? _ctx;
    private string _connectionString = "";
    private IGrainTimer? _timer;
    private int _stableTicks;
    private readonly IMessageBus _bus;
    private readonly IDocumentStore _store;
    private readonly ILogger<WorldGrain> _log;

    public WorldGrain(IMessageBus bus, IDocumentStore store, ILogger<WorldGrain> log)
    {
        _bus = bus; _store = store; _log = log;
    }

    public async ValueTask StartAsync(string connectionString)
    {
        _connectionString = connectionString;
        _ctx = await LoadStateAsync();
        _timer = this.RegisterGrainTimer(OnTick, new GrainTimerCreationOptions
        {
            DueTime = TimeSpan.FromMilliseconds(100),
            Period = TimeSpan.FromMilliseconds(100),
        });
    }

    public async ValueTask<TickResult> InjectAsync(Input input)
    {
        if (_ctx is null) throw new InvalidOperationException("World not started");
        _stableTicks = 0;
        return await RunTickAsync([input]);
    }

    public async ValueTask<TickResult> TickAsync()
    {
        if (_ctx is null) throw new InvalidOperationException("World not started");
        return await RunTickAsync([]);
    }

    private async Task OnTick()
    {
        if (_ctx is null) return;

        if (_ctx.Stable && _stableTicks++ > 10)
        {
            _timer?.Dispose();
            _timer = this.RegisterGrainTimer(OnTick, new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromSeconds(5),
                Period = TimeSpan.FromSeconds(5),
            });
            return;
        }

        await RunTickAsync([]);
    }

    private async Task<TickResult> RunTickAsync(IReadOnlyList<Input> inputs)
    {
        var result = TickPipeline.Run(_ctx!, inputs);

        if (result.Events.Length > 0)
        {
            await using var session = _store.LightweightSession();
            session.Events.Append(this.GetGrainId().GetGuidKey(), result.Events.Cast<object>().ToArray());
            await session.SaveChangesAsync();
        }

        if (result.Pending.Length > 0)
            await SideEffectDispatcher.DispatchAsync(_bus, this.GetGrainId().GetGuidKey(), result.Pending);

        if (result.Protocol.Length > 0)
            await WriteProtocolAsync(result.Protocol);

        return result;
    }

    private async Task<TickCtx> LoadStateAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var subjectId = this.GetGrainId().GetGuidKey();

        var signals = await LoadSignalsAsync(conn, subjectId);
        var edges = await LoadEdgesAsync(conn, subjectId);
        var gates = await LoadGatesAsync(conn, subjectId);

        var idToIdx = signals.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var remappedSignals = signals.Select((s, i) => s with { Id = i }).ToArray();
        var remappedEdges = edges.Select(e => e with
        {
            SourceId = idToIdx.GetValueOrDefault(e.SourceId, -1),
            TargetId = idToIdx.GetValueOrDefault(e.TargetId, -1),
        }).Where(e => e.SourceId >= 0 && e.TargetId >= 0).ToArray();

        return new TickCtx
        {
            Signals = new SignalColumns(remappedSignals),
            Edges = remappedEdges,
            Gates = gates,
            TopoLevels = GraphUtils.ComputeTopoLevels(remappedSignals.Length, remappedEdges),
        };
    }

    private static async Task<SignalRow[]> LoadSignalsAsync(NpgsqlConnection conn, Guid subjectId)
    {
        var rows = new List<SignalRow>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT ON (code) id, code, state, COALESCE(value, 0), COALESCE(baseline, 0),
                   confidence, COALESCE(distribution, 'N'),
                   COALESCE(tau_min_ms, 0), COALESCE(tau_max_ms, 0),
                   COALESCE(range_low, 0), COALESCE(range_high, 999999)
            FROM signal WHERE entity_id = @sid ORDER BY code, created_on_utc DESC", conn);
        cmd.Parameters.AddWithValue("sid", subjectId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new SignalRow(reader.GetInt32(0), reader.GetString(1), null,
                reader.GetString(2), reader.GetDouble(3), reader.GetDouble(4),
                reader.GetDouble(5), reader.GetString(6),
                reader.GetDouble(7), reader.GetDouble(8),
                reader.GetDouble(9), reader.GetDouble(10)));
        }
        return [.. rows];
    }

    private static async Task<EdgeRow[]> LoadEdgesAsync(NpgsqlConnection conn, Guid subjectId)
    {
        var rows = new List<EdgeRow>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, source_id, target_id, operator, operator_class,
                   COALESCE(gain, 1), COALESCE(noise_sigma, 0), COALESCE(transfer_fn, 'lin'),
                   COALESCE(delay_ms, 0), clamp_lo, clamp_hi, gate_id, tool_id, active
            FROM edge WHERE entity_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", subjectId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new EdgeRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4),
                reader.GetDouble(5), reader.GetDouble(6), reader.GetString(7),
                reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetDouble(9),
                reader.IsDBNull(10) ? null : reader.GetDouble(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.GetBoolean(13)));
        }
        return [.. rows];
    }

    private static async Task<GateRow[]> LoadGatesAsync(NpgsqlConnection conn, Guid subjectId)
    {
        var rows = new List<GateRow>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, code, type, threshold, expression, probability, latched,
                   prompt, model, parse_map, fallback_expr, timeout_ms, cache_ms
            FROM gate WHERE entity_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", subjectId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new GateRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12)));
        }
        return [.. rows];
    }

    private async Task WriteProtocolAsync(ProtocolEntry[] entries)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var e in entries)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO protocol (entity_id, tag, formula, confidence)
                VALUES (@sid, @tag, @formula, @conf)", conn);
            cmd.Parameters.AddWithValue("sid", this.GetGrainId().GetGuidKey());
            cmd.Parameters.AddWithValue("tag", e.Tag);
            cmd.Parameters.AddWithValue("formula", e.Content);
            cmd.Parameters.AddWithValue("conf", (object?)e.Confidence ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public ValueTask StopAsync() { _timer?.Dispose(); return ValueTask.CompletedTask; }
}

// ──────────────────────── SIGNALR HUB ────────────────────────

public sealed class WorldHub : Hub
{
    public async Task SubscribeToWorld(string worldId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, worldId);

    public async Task UnsubscribeFromWorld(string worldId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, worldId);
}

// ──────────────────────── FLUENT BUILDER ────────────────────────

public sealed class WorldBuilder
{
    private readonly IClusterClient _orleans;
    private readonly string _connectionString;
    private IVocabulary? _vocab;
    private string? _bnfSource;

    public WorldBuilder(IClusterClient orleans, string connectionString)
    {
        _orleans = orleans; _connectionString = connectionString;
    }

    public WorldBuilder WithVocabulary(IVocabulary vocab) { _vocab = vocab; return this; }
    public WorldBuilder FromBnf(string bnfText) { _bnfSource = bnfText; return this; }
    public WorldBuilder FromFile(string path) { _bnfSource = File.ReadAllText(path); return this; }

    public async Task<Guid> BuildAsync()
    {
        var worldId = Guid.NewGuid();

        if (_bnfSource is not null)
        {
            var result = SignalsCompiler.Compile(_bnfSource, worldId, _vocab);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            foreach (var sql in result.SqlStatements)
            {
                if (sql.StartsWith("--")) continue;
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var grain = _orleans.GetGrain<IWorldGrain>(worldId);
        await grain.StartAsync(_connectionString);

        return worldId;
    }
}

// ──────────────────────── SERVICE REGISTRATION ────────────────────────

public static class SignalsKernelExtensions
{
    public static IHostApplicationBuilder AddSignalsKernel(this IHostApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("biochain")
            ?? throw new InvalidOperationException("ConnectionStrings:biochain not configured");

        builder.Services.AddMarten(opts =>
        {
            opts.Connection(connStr);
        }).UseLightweightSessions();

        builder.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(LlmBridge).Assembly);
        });

        var neo4jUri = builder.Configuration["Neo4j:Uri"];
        if (neo4jUri is not null)
        {
            builder.Services.AddSingleton<Neo4jTickSync>();
            builder.Services.AddSingleton<GdsAnalysis>();
        }

        builder.Services.AddSignalR();

        builder.Services.AddTransient(sp =>
        {
            var orleans = sp.GetRequiredService<IClusterClient>();
            return new WorldBuilder(orleans, connStr);
        });

        return builder;
    }
}
