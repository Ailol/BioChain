using SpacetimeDB.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.ClientApi;

namespace BioChain.Agent;

/// <summary>
/// Manages WebSocket connection to SpacetimeDB and provides typed access
/// to reducers (via SDK event callbacks) and tables (via local client cache).
/// All HTTP/SQL infrastructure has been removed — the SDK handles everything.
/// </summary>
public sealed class SpacetimeService : IAsyncDisposable
{
    private readonly ILogger<SpacetimeService> _logger;
    private readonly SpacetimeOptions _options;
    private DbConnection? _conn;
    private readonly TaskCompletionSource _subscribed = new();
    private CancellationTokenSource? _tickCts;
    private Thread? _tickThread;
    // Note: no lock needed — FrameTick runs on its own thread and SDK handles thread safety

    public DbConnection Conn => _conn ?? throw new InvalidOperationException("Not connected");

    public SpacetimeService(IOptions<SpacetimeOptions> options, ILogger<SpacetimeService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Connect to SpacetimeDB via WebSocket, subscribe to all tables,
    /// and start the FrameTick pump. Throws on failure.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _conn = DbConnection.Builder()
            .WithUri(_options.Host)
            .WithDatabaseName(_options.Database)
            .OnConnect((conn, identity, token) =>
            {
                _logger.LogInformation("WS connected to SpacetimeDB {Db} as {Identity}",
                    _options.Database, identity);

                conn.SubscriptionBuilder()
                    .OnApplied((_) =>
                    {
                        _logger.LogInformation("Subscription applied, client cache ready");
                        _subscribed.TrySetResult();
                    })
                    .OnError((ctx, err) =>
                    {
                        _logger.LogError("Subscription error: {Error}", err.Message);
                    })
                    .Subscribe(new string[]
                    {
                        "SELECT * FROM program",
                        "SELECT * FROM node",
                        "SELECT * FROM edge",
                        "SELECT * FROM diag",
                        "SELECT * FROM tensor",
                        "SELECT * FROM delta_op",
                        "SELECT * FROM delta_log",
                        "SELECT * FROM meta_op",
                        "SELECT * FROM conv",
                        "SELECT * FROM sim_run",
                        "SELECT * FROM sim_tick",
                        "SELECT * FROM snapshot",
                        "SELECT * FROM snapshot_edge",
                        "SELECT * FROM snapshot_node",
                        "SELECT * FROM diff_result",
                        "SELECT * FROM tau_acc",
                    });
            })
            .OnConnectError((err) =>
            {
                _logger.LogError("WS connection to SpacetimeDB failed: {Error}", err);
                _subscribed.TrySetException(
                    new InvalidOperationException($"SpacetimeDB connection failed: {err}"));
            })
            .OnDisconnect((conn, err) =>
            {
                _logger.LogWarning("WS disconnected from SpacetimeDB: {Error}", err?.Message);
            })
            .Build();

        _conn.OnUnhandledReducerError += (ctx, ex) =>
        {
            _logger.LogError(ex, "Unhandled reducer error");
        };

        // Start the FrameTick pump on a dedicated thread
        _tickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _tickThread = new Thread(() => TickLoop(_tickCts.Token))
        {
            IsBackground = true,
            Name = "SpacetimeDB-FrameTick"
        };
        _tickThread.Start();

        // Wait for subscription to be applied (with timeout)
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        await _subscribed.Task.WaitAsync(cts.Token);
    }

    private int _tickCount;

    private void TickLoop(CancellationToken ct)
    {
        _logger.LogInformation("FrameTick loop started");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _conn?.FrameTick();
                _tickCount++;
                if (_tickCount % 600 == 0) // every ~10 seconds
                    _logger.LogDebug("FrameTick alive: {Count} ticks, connected={Connected}",
                        _tickCount, _conn?.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FrameTick");
            }
            Thread.Sleep(16);
        }
        _logger.LogInformation("FrameTick loop stopped");
    }

    // ── Reducer methods (TCS bridge pattern) ────────────────────────

    /// <summary>
    /// Manually pump FrameTick from the calling thread to ensure messages are processed.
    /// </summary>
    private void PumpFrameTick(int iterations = 10, int delayMs = 50)
    {
        for (int i = 0; i < iterations; i++)
        {
            _conn?.FrameTick();
            Thread.Sleep(delayMs);
        }
    }

    public async Task<ulong> CreateProgramAsync(string name, string? phase, List<string> domains)
    {
        _logger.LogInformation("Calling CreateProgram({Name})", name);

        // Call reducer
        Conn.Reducers.CreateProgram(name, phase, domains);

        // Poll the local cache for the insert to appear
        for (int i = 0; i < 300; i++) // up to ~30 seconds
        {
            // Pump FrameTick from this thread to ensure WS messages are processed
            _conn?.FrameTick();
            await Task.Delay(100);

            foreach (var p in Conn.Db.Program.Iter())
            {
                if (p.Name == name)
                {
                    _logger.LogInformation("CreateProgram completed: Id={Id}", p.Id);
                    return p.Id;
                }
            }
        }

        _logger.LogError("CreateProgram timed out after 30s — program '{Name}' not found in cache", name);
        throw new TimeoutException($"CreateProgram timed out for '{name}'");
    }

    public async Task IngestBnfAsync(ulong programId, string pipeline, string bnfText)
    {
        _logger.LogInformation("Calling IngestBnf({ProgramId}, {Pipeline}, {Len} chars)",
            programId, pipeline, bnfText.Length);

        // Store raw first so we can track it
        Conn.Reducers.StoreRawBnf(programId, pipeline, bnfText);
        await WaitForRawBnfAsync(programId, pipeline);

        // Now ingest (parse + insert nodes/edges)
        Conn.Reducers.IngestBnf(programId, pipeline, bnfText);
        await WaitForNodesAsync(programId);

        var nodeCount = Conn.Db.Node.ByProgram.Filter(programId).Count();
        var edgeCount = Conn.Db.Edge.ByProgram.Filter(programId).Count();

        if (nodeCount == 0)
            _logger.LogWarning("IngestBnf produced 0 nodes — check module logs for parse errors");

        _logger.LogInformation("IngestBnf completed: {NodeCount} nodes, {EdgeCount} edges",
            nodeCount, edgeCount);
    }

    private async Task WaitForRawBnfAsync(ulong programId, string pipeline)
    {
        for (int i = 0; i < 300; i++)
        {
            _conn?.FrameTick();
            await Task.Delay(100);
            var p = Conn.Db.Program.Id.Find(programId);
            if (p != null)
            {
                var raw = pipeline switch
                {
                    "base" => p.RawBase,
                    "plasticity" => p.RawPlasticity,
                    "meta" => p.RawMeta,
                    "convergence" => p.RawConvergence,
                    _ => null
                };
                if (raw != null) return;
            }
        }
        throw new TimeoutException($"StoreRawBnf timed out for program {programId}");
    }

    private async Task WaitForNodesAsync(ulong programId)
    {
        for (int i = 0; i < 300; i++)
        {
            _conn?.FrameTick();
            await Task.Delay(100);
            if (Conn.Db.Node.ByProgram.Filter(programId).Any())
                return;
        }
        _logger.LogWarning("WaitForNodes timed out — 0 nodes after 30s for program {ProgramId}", programId);
    }

    public async Task ValidateAsync(ulong programId)
    {
        _logger.LogInformation("Calling Validate({ProgramId})", programId);

        var diagsBefore = Conn.Db.Diag.ByProgram.Filter(programId).Count();
        Conn.Reducers.Validate(programId);

        // Poll for diag count to change (or for a short delay to allow processing)
        for (int i = 0; i < 100; i++)
        {
            _conn?.FrameTick();
            await Task.Delay(100);
            var diagsNow = Conn.Db.Diag.ByProgram.Filter(programId).Count();
            if (diagsNow != diagsBefore)
            {
                _logger.LogInformation("Validate completed: {DiagCount} diagnostics", diagsNow);
                return;
            }
        }
        // Even if diags didn't change, validation may have run with no issues
        _logger.LogInformation("Validate completed (no new diagnostics)");
    }

    public async Task SimulateAsync(ulong programId, List<Perturbation> perturbations, uint maxTicks = 1000)
    {
        _logger.LogInformation("Calling Simulate({ProgramId}, {MaxTicks} ticks)", programId, maxTicks);

        var simRunsBefore = Conn.Db.SimRun.ByProgram.Filter(programId).Count();
        Conn.Reducers.Simulate(programId, perturbations, maxTicks);

        // Poll for new SimRun to appear
        for (int i = 0; i < 600; i++) // up to 60 seconds
        {
            _conn?.FrameTick();
            await Task.Delay(100);
            var simRunsNow = Conn.Db.SimRun.ByProgram.Filter(programId).Count();
            if (simRunsNow > simRunsBefore)
            {
                _logger.LogInformation("Simulate completed");
                return;
            }
        }
        _logger.LogWarning("Simulate may have timed out for program {ProgramId}", programId);
    }

    // ── Read methods (local client cache) ───────────────────────────

    public List<Node> GetNodes(ulong programId)
    {
        return Conn.Db.Node.ByProgram.Filter(programId).ToList();
    }

    public List<Edge> GetEdges(ulong programId)
    {
        return Conn.Db.Edge.ByProgram.Filter(programId).ToList();
    }

    public List<Diag> GetDiags(ulong programId)
    {
        return Conn.Db.Diag.ByProgram.Filter(programId).ToList();
    }

    public List<SimRun> GetSimRuns(ulong programId)
    {
        return Conn.Db.SimRun.ByProgram.Filter(programId).ToList();
    }

    public int GetNodeCount(ulong programId)
    {
        return Conn.Db.Node.ByProgram.Filter(programId).Count();
    }

    public int GetEdgeCount(ulong programId)
    {
        return Conn.Db.Edge.ByProgram.Filter(programId).Count();
    }

    public List<string> GetDiagStrings(ulong programId)
    {
        return Conn.Db.Diag.ByProgram.Filter(programId)
                .Select(d => $"{d.Kind}: {d.Expr}")
                .ToList();
    }

    public List<Dictionary<string, string>> SearchNodes(ulong programId, string codePattern)
    {
        return Conn.Db.Node.ByProgram.Filter(programId)
                .Where(n => n.Code.Contains(codePattern, StringComparison.OrdinalIgnoreCase))
                .Select(n => new Dictionary<string, string>
                {
                    ["code"] = n.Code,
                    ["kind"] = n.Kind,
                    ["region"] = n.Region ?? "",
                    ["state"] = n.State?.Sym ?? ""
                })
                .ToList();
    }

    /// <summary>
    /// Fetch the raw BNF text stored on a program for each pipeline layer.
    /// </summary>
    public Dictionary<string, string?> GetProgramRawBnf(ulong programId)
    {
        var program = Conn.Db.Program.Id.Find(programId);

        if (program is null)
            throw new InvalidOperationException($"Program {programId} not found");

        return new Dictionary<string, string?>
        {
            ["base"] = program.RawBase,
            ["plasticity"] = program.RawPlasticity,
            ["meta"] = program.RawMeta,
            ["convergence"] = program.RawConvergence,
        };
    }

    /// <summary>
    /// Build a compact text summary of a program's biochemical network for LLM context.
    /// </summary>
    public string GetProgramContext(ulong programId)
    {
        var program = Conn.Db.Program.Id.Find(programId);
        var nodes = Conn.Db.Node.ByProgram.Filter(programId).ToList();
        var edges = Conn.Db.Edge.ByProgram.Filter(programId).ToList();
        var diags = Conn.Db.Diag.ByProgram.Filter(programId).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Biochemical Network (Program {programId})");
        if (program is not null)
            sb.AppendLine($"Name: {program.Name}");
        sb.AppendLine();

        // Raw BNF layers (full network description from LLM)
        if (program is not null)
        {
            var layers = new[] {
                ("Base", program.RawBase),
                ("Plasticity", program.RawPlasticity),
                ("Meta", program.RawMeta),
                ("Convergence", program.RawConvergence)
            };
            foreach (var (name, raw) in layers)
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    sb.AppendLine($"## {name} Layer");
                    sb.AppendLine(raw);
                    sb.AppendLine();
                }
            }
        }

        // Parsed graph structure
        sb.AppendLine($"## Parsed Nodes ({nodes.Count})");
        var idToCode = new Dictionary<ulong, string>();
        foreach (var n in nodes)
        {
            idToCode[n.Id] = n.Code;
            var line = $"- {n.Code} ({n.Kind})";
            if (!string.IsNullOrEmpty(n.Region)) line += $" @{n.Region}";
            if (n.State is not null) line += $" [{n.State.Sym}]";
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine($"## Parsed Edges ({edges.Count})");
        foreach (var e in edges)
        {
            var src = idToCode.GetValueOrDefault(e.SourceId, $"#{e.SourceId}");
            var tgt = idToCode.GetValueOrDefault(e.TargetId, $"#{e.TargetId}");
            var edgeType = e.EdgeType ?? "→";
            sb.AppendLine($"- {src} {edgeType} {tgt}");
        }

        sb.AppendLine();
        sb.AppendLine($"## Diagnostics ({diags.Count})");
        foreach (var d in diags)
            sb.AppendLine($"- {d.Kind}: {d.Expr}");

        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _tickCts?.Cancel();
        _tickThread?.Join(2000);
        _conn?.Disconnect();
        _conn = null;
        _tickCts?.Dispose();
    }
}
