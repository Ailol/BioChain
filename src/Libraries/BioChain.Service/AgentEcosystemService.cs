using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;
using BioChain.Service.Models;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BioChain.Service;

/// <summary>
/// Evolving agent ecosystem. Listens for graph changes, evaluates MODULE gates,
/// extracts subgraphs, calls LLM for evolution, parses output back into graph.
///
/// Architecture:
///   PG INSERT → graph_changed NOTIFY
///     → debounce per subject_id
///     → load active MODULEs for subject
///     → for each MODULE whose watched signals changed:
///         → evaluate gate (PG function)
///         → extract subgraph (PG serialize_profile_dsl or Neo4j Cypher)
///         → load prediction history
///         → call LLM with evolution prompt
///         → parse output → LinkComponentPublicAsync → write back to graph
///         → update module lifecycle (eval_count, hit_count, utility)
///     → cycle continues (new writes may trigger further evaluations)
/// </summary>
public sealed class AgentEcosystemService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatClient _engine;
    private readonly string _pgConnString;
    private readonly int _debounceMs;
    private readonly ILogger<AgentEcosystemService> _logger;
    private readonly bool _hasNeo4j;

    // Debounce: subject_id → CTS
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pending = new();

    // Track which signal codes changed per subject (accumulated during debounce window)
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<string>> _changedSignals = new();

    private static readonly string EvolutionPrompt = LoadEvolutionPrompt();

    public AgentEcosystemService(
        IServiceScopeFactory scopeFactory,
        IChatClient engine,
        IConfiguration config,
        ILogger<AgentEcosystemService> logger)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
        _pgConnString = config.GetConnectionString("personality")
            ?? throw new InvalidOperationException("ConnectionStrings:personality is required");
        _debounceMs = int.TryParse(config["AgentEcosystem:DebounceMs"], out var d) ? d : 2000;
        _hasNeo4j = !string.IsNullOrEmpty(config["Neo4j:Uri"]);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AgentEcosystem] Starting — debounce={DebounceMs}ms, neo4j={HasNeo4j}",
            _debounceMs, _hasNeo4j);

        await ListenLoopAsync(stoppingToken);
    }

    // ── LISTEN Loop (same pattern as GraphSyncService) ───────────────────────

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
                delay = 1;

                _logger.LogInformation("[AgentEcosystem] LISTEN connected");

                while (!ct.IsCancellationRequested)
                    await conn.WaitAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[AgentEcosystem] LISTEN connection lost, reconnecting in {Delay}s", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                delay = Math.Min(delay * 2, 30);
            }
        }
    }

    // ── Notification → Debounce ──────────────────────────────────────────────

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.Payload);
            var root = doc.RootElement;

            var subjectIdStr = root.GetProperty("entity_id").GetString();
            if (!Guid.TryParse(subjectIdStr, out var subjectId)) return;

            // Accumulate which signal/table changed during debounce window
            if (root.TryGetProperty("code", out var c) && c.GetString() is { } code)
            {
                var bag = _changedSignals.GetOrAdd(subjectId, _ => []);
                bag.Add(code);
            }

            ScheduleEvaluation(subjectId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AgentEcosystem] Failed to parse notification: {Payload}", e.Payload);
        }
    }

    private void ScheduleEvaluation(Guid subjectId)
    {
        // Cancel any existing debounce timer
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

                // Collect accumulated signal codes
                var changedCodes = Array.Empty<string>();
                if (_changedSignals.TryRemove(subjectId, out var bag))
                    changedCodes = bag.Distinct().ToArray();

                await EvaluateModulesAsync(subjectId, changedCodes);
            }
            catch (OperationCanceledException)
            {
                // Debounce reset — expected
            }
            catch (Exception ex)
            {
                _pending.TryRemove(subjectId, out _);
                _logger.LogError(ex, "[AgentEcosystem] Evaluation failed for subject {SubjectId}", subjectId);
            }
        });
    }

    // ── Core Evaluation Loop ─────────────────────────────────────────────────

    private async Task EvaluateModulesAsync(Guid subjectId, string[] changedCodes)
    {
        using var scope = _scopeFactory.CreateScope();
        var moduleRepo = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var protocolRepo = scope.ServiceProvider.GetRequiredService<IProtocolRepository>();
        var analyzeService = scope.ServiceProvider.GetRequiredService<AnalyzeService>();

        var allModules = await moduleRepo.GetBySubjectAsync(subjectId);

        foreach (var module in allModules)
        {
            try
            {
                // 1. Deserialize lifecycle properties
                var props = DeserializeProps(module.Properties);
                if (props.Status != "active") continue;

                // 2. Check if changed signals overlap with this module's watch list
                if (props.WatchSignals.Length > 0 && changedCodes.Length > 0)
                {
                    var overlap = props.WatchSignals
                        .Intersect(changedCodes, StringComparer.OrdinalIgnoreCase)
                        .Any();
                    if (!overlap) continue;
                }
                else if (props.WatchSignals.Length > 0)
                {
                    // Module has a watch list but we don't know what changed — skip
                    continue;
                }
                // If no watch list, evaluate on any change (new module without filter)

                // 3. Evaluate gate via PG function (if module has a gate)
                var gateActive = await EvaluateModuleGateAsync(module, subjectId, scope);
                if (!gateActive) continue;

                // 4. Extract subgraph context
                var subgraphDsl = await ExtractSubgraphAsync(subjectId, props.WatchSignals, scope);

                // 5. Load prediction history
                var predictions = await protocolRepo.GetByModuleTagAsync(module.Id, "PREDICTION");

                // 6. Build evolution context and call LLM
                var userContext = BuildEvolutionContext(module, props, subgraphDsl, predictions);
                var response = await _engine.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, EvolutionPrompt),
                    new ChatMessage(ChatRole.User, userContext),
                ]);
                var outputText = response.Text ?? "";

                _logger.LogDebug("[AgentEcosystem] Module {Code} LLM output: {Output}",
                    module.Code, outputText[..Math.Min(200, outputText.Length)]);

                // 7. Skip if no changes
                if (outputText.Contains("-- no changes", StringComparison.OrdinalIgnoreCase))
                {
                    props.EvalCount++;
                    props.LastEval = DateTimeOffset.UtcNow;
                    await moduleRepo.UpdatePropertiesAsync(module.Id, JsonSerializer.Serialize(props));
                    continue;
                }

                // 8. Parse LLM output through existing pipeline
                var lines = BioChainParser.Parse(outputText);
                if (lines.Count > 0)
                {
                    // Create a stimuli entry to anchor the protocols
                    var stimuliRepo = scope.ServiceProvider.GetRequiredService<IStimuliRepository>();
                    var stimuliEntity = new StimuliEntity
                    {
                        SubjectId = subjectId,
                        Kind = "agent_evolution",
                        SourceText = $"MODULE:{module.Code} eval #{props.EvalCount + 1}",
                        Analyzed = true,
                    };
                    stimuliEntity = await stimuliRepo.CreateAsync(stimuliEntity);

                    // Create an anchor protocol for this evolution cycle
                    var anchorProtocol = new ProtocolEntity
                    {
                        SubjectId = subjectId,
                        StimuliId = stimuliEntity.Id,
                        ModuleId = module.Id,
                        Tag = "EVOLUTION",
                        Formula = $"MODULE:{module.Code} generation={props.Generation}",
                    };
                    anchorProtocol = await protocolRepo.CreateAsync(anchorProtocol);

                    foreach (var line in lines)
                    {
                        await analyzeService.LinkComponentPublicAsync(
                            anchorProtocol, line, subjectId, default);
                    }
                }

                // 9. Update module lifecycle
                var confirmedCount = lines
                    .Where(l => l.Tag == "PREDICTION")
                    .Count(l => l.Status?.Contains("confirmed", StringComparison.OrdinalIgnoreCase) == true);

                props.EvalCount++;
                props.HitCount += confirmedCount;
                props.Utility = props.EvalCount > 0
                    ? (double)props.HitCount / props.EvalCount
                    : 0.5;
                props.LastEval = DateTimeOffset.UtcNow;

                // Auto-retire if utility drops too low after sufficient evaluations
                if (props.EvalCount >= 10 && props.Utility < 0.1)
                {
                    props.Status = "retired";
                    _logger.LogInformation("[AgentEcosystem] Module {Code} retired (utility={Utility:F2})",
                        module.Code, props.Utility);
                }

                await moduleRepo.UpdatePropertiesAsync(module.Id, JsonSerializer.Serialize(props));

                _logger.LogInformation(
                    "[AgentEcosystem] Module {Code} evaluated: {LineCount} outputs, utility={Utility:F2}",
                    module.Code, lines.Count, props.Utility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentEcosystem] Error evaluating module {ModuleId} ({Code})",
                    module.Id, module.Code);
            }
        }

        // Periodically check for new patterns to spawn MODULEs
        try
        {
            await SpawnModulesForPatternsAsync(subjectId, scope);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AgentEcosystem] Pattern spawning failed for subject {SubjectId}", subjectId);
        }
    }

    // ── Gate Evaluation via PG ───────────────────────────────────────────────

    private async Task<bool> EvaluateModuleGateAsync(
        ModuleEntity module, Guid subjectId, IServiceScope scope)
    {
        var gateRepo = scope.ServiceProvider.GetRequiredService<IGateRepository>();
        var moduleGates = await gateRepo.GetByPersonAsync(subjectId);
        var gate = moduleGates.FirstOrDefault(g => g.ModuleId == module.Id);

        if (gate is null) return true; // No gate = always active

        // Evaluate via PG function
        await using var conn = new NpgsqlConnection(_pgConnString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT evaluate_gate($1, $2)", conn);
        cmd.Parameters.AddWithValue(gate.Id);
        cmd.Parameters.AddWithValue(subjectId);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    // ── Subgraph Extraction ──────────────────────────────────────────────────

    private async Task<string> ExtractSubgraphAsync(
        Guid subjectId, string[] watchSignals, IServiceScope scope)
    {
        // Try Neo4j targeted extraction if available and we have specific signals
        if (_hasNeo4j && watchSignals.Length > 0)
        {
            try
            {
                var driver = scope.ServiceProvider.GetService<Neo4j.Driver.IDriver>();
                if (driver is not null)
                    return await ExtractSubgraphNeo4jAsync(driver, subjectId, watchSignals);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AgentEcosystem] Neo4j extraction failed, falling back to PG DSL");
            }
        }

        // Fallback: PG serialize_profile_dsl() — full profile DSL
        await using var conn = new NpgsqlConnection(_pgConnString);
        await conn.OpenAsync();

        await using (var refreshCmd = new NpgsqlCommand("SELECT refresh_graph()", conn))
            await refreshCmd.ExecuteNonQueryAsync();

        await using var cmd = new NpgsqlCommand("SELECT serialize_profile_dsl($1)", conn);
        cmd.Parameters.AddWithValue(subjectId);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "(empty graph)";
    }

    private static async Task<string> ExtractSubgraphNeo4jAsync(
        Neo4j.Driver.IDriver driver, Guid subjectId, string[] signalCodes)
    {
        const string query = """
            MATCH (s {subject_id: $pid})
            WHERE s.code IN $codes
            OPTIONAL MATCH (s)-[r]-(n1)
            OPTIONAL MATCH (n1)-[r2]-(n2)
            WHERE n2 <> s
            WITH collect(DISTINCT s) + collect(DISTINCT n1) + collect(DISTINCT n2) AS allNodes,
                 collect(DISTINCT r) + collect(DISTINCT r2) AS allRels
            UNWIND allNodes AS node
            WITH DISTINCT node, allRels
            WHERE node IS NOT NULL
            RETURN collect(DISTINCT {
                kind: labels(node)[0],
                code: node.code,
                state: node.state
            }) AS nodes,
            [rel IN allRels WHERE rel IS NOT NULL |
                {
                    source: startNode(rel).code,
                    target: endNode(rel).code,
                    operator: type(rel)
                }
            ] AS edges
            """;

        await using var session = driver.AsyncSession();
        var result = await session.RunAsync(query, new Dictionary<string, object>
        {
            ["pid"] = subjectId.ToString(),
            ["codes"] = signalCodes
        });

        var record = await result.SingleAsync();

        var sb = new StringBuilder();
        sb.AppendLine("# Subgraph (2-hop neighborhood)");
        sb.AppendLine();

        if (record["nodes"] is IList<object> nodes)
        {
            foreach (var item in nodes)
            {
                if (item is IDictionary<string, object> n)
                {
                    n.TryGetValue("kind", out var kind);
                    n.TryGetValue("code", out var code);
                    n.TryGetValue("state", out var state);
                    sb.AppendLine($"  {kind}:{code} [{state ?? "\u2248"}]");
                }
            }
        }

        sb.AppendLine();

        if (record["edges"] is IList<object> edges)
        {
            foreach (var item in edges)
            {
                if (item is IDictionary<string, object> e)
                {
                    e.TryGetValue("source", out var src);
                    e.TryGetValue("target", out var tgt);
                    e.TryGetValue("operator", out var op);
                    sb.AppendLine($"  {src} \u2192 {tgt} [{op}]");
                }
            }
        }

        return sb.ToString();
    }

    // ── Pattern Detection (Neo4j Cypher) ─────────────────────────────────────

    private async Task<List<string>> DetectPatternsAsync(Guid subjectId, IServiceScope scope)
    {
        if (!_hasNeo4j) return [];

        var driver = scope.ServiceProvider.GetService<Neo4j.Driver.IDriver>();
        if (driver is null) return [];

        var patterns = new List<string>();
        await using var session = driver.AsyncSession();

        // 1. Positive feedback loops with elevated signals
        try
        {
            var feedbackResult = await session.RunAsync("""
                MATCH (a)-[r:FEEDBACK]->(b)
                WHERE a.subject_id = $pid
                  AND a.state IN ['\u2191', '\u2191\u2191']
                  AND r.properties CONTAINS '\u27f3\u207a'
                RETURN a.code AS source, b.code AS target, a.state AS state
                LIMIT 10
                """, new Dictionary<string, object> { ["pid"] = subjectId.ToString() });

            var records = await feedbackResult.ToListAsync();
            foreach (var r in records)
                patterns.Add($"FEEDBACK_LOOP: {r["source"]}->{r["target"]} ({r["state"]})");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[AgentEcosystem] Feedback loop detection query failed");
        }

        // 2. Long cascade paths (depth >= 3)
        try
        {
            var cascadeResult = await session.RunAsync("""
                MATCH path = (a)-[*3..5]->(b)
                WHERE a.subject_id = $pid
                  AND a.state IN ['\u2191\u2191', '\u2193\u2193']
                RETURN [n IN nodes(path) | n.code] AS cascade, length(path) AS depth
                LIMIT 5
                """, new Dictionary<string, object> { ["pid"] = subjectId.ToString() });

            var records = await cascadeResult.ToListAsync();
            foreach (var r in records)
            {
                var cascadeCodes = r["cascade"] is IList<object> list
                    ? list.Select(x => x?.ToString() ?? "?")
                    : [];
                var cascade = string.Join(" \u2192 ", cascadeCodes);
                patterns.Add($"CASCADE: {cascade} (depth={r["depth"]})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[AgentEcosystem] Cascade detection query failed");
        }

        return patterns;
    }

    // ── Module Spawning ──────────────────────────────────────────────────────

    private async Task SpawnModulesForPatternsAsync(Guid subjectId, IServiceScope scope)
    {
        var moduleRepo = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var existingModules = await moduleRepo.GetBySubjectAsync(subjectId);

        var patterns = await DetectPatternsAsync(subjectId, scope);
        if (patterns.Count == 0) return;

        foreach (var pattern in patterns)
        {
            var signalCodes = ExtractSignalCodesFromPattern(pattern);

            // Check if any existing module already watches these signals
            var alreadyCovered = existingModules.Any(m =>
            {
                var p = DeserializeProps(m.Properties);
                return p.Status == "active" &&
                       signalCodes.All(c =>
                           p.WatchSignals.Contains(c, StringComparer.OrdinalIgnoreCase));
            });

            if (alreadyCovered) continue;

            var agentType = pattern.StartsWith("FEEDBACK_LOOP") ? "MONITOR" : "DIAGNOSER";
            var code = $"auto_{agentType.ToLower()}_{signalCodes.FirstOrDefault() ?? "unknown"}";
            if (code.Length > 50) code = code[..50];

            var props = new ModuleProps
            {
                Status = "active",
                Generation = 0,
                WatchSignals = signalCodes,
            };

            var entity = new ModuleEntity
            {
                SubjectId = subjectId,
                Code = code,
                AgentType = agentType,
                Properties = JsonSerializer.Serialize(props),
            };

            await moduleRepo.CreateAsync(entity);
            _logger.LogInformation("[AgentEcosystem] Spawned MODULE {Code} for pattern: {Pattern}",
                code, pattern[..Math.Min(100, pattern.Length)]);
        }
    }

    private static string[] ExtractSignalCodesFromPattern(string pattern)
    {
        var codes = new List<string>();
        var parts = pattern.Split([':', '\u2192', '-', '>', '(', ')', ' '],
            StringSplitOptions.RemoveEmptyEntries);

        var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FEEDBACK", "LOOP", "CASCADE", "MONITOR", "DIAGNOSER",
            "FEEDBACK_LOOP", "depth", "status"
        };

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length >= 2 && trimmed.Length <= 30 &&
                trimmed.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_') &&
                trimmed.Any(char.IsUpper) &&
                !skipWords.Contains(trimmed))
            {
                codes.Add(trimmed);
            }
        }

        return codes.Distinct().Take(5).ToArray();
    }

    // ── Build LLM Context ────────────────────────────────────────────────────

    private static string BuildEvolutionContext(
        ModuleEntity module,
        ModuleProps props,
        string subgraphDsl,
        List<ProtocolEntity> predictions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## MODULE");
        sb.AppendLine($"Code: {module.Code}");
        sb.AppendLine($"Agent Type: {module.AgentType ?? "REACTIVE"}");
        sb.AppendLine($"Generation: {props.Generation}");
        sb.AppendLine($"Utility: {props.Utility:F2} ({props.HitCount}/{props.EvalCount} hits)");
        if (props.WatchSignals.Length > 0)
            sb.AppendLine($"Watch Signals: {string.Join(", ", props.WatchSignals)}");
        sb.AppendLine();

        sb.AppendLine("## CURRENT GRAPH STATE");
        sb.AppendLine(subgraphDsl);
        sb.AppendLine();

        if (predictions.Count > 0)
        {
            sb.AppendLine("## PREDICTION HISTORY");
            foreach (var p in predictions.Take(20)) // Limit context window
                sb.AppendLine($"- [{p.Status ?? "pending"}] {p.Formula} (created: {p.CreatedOnUtc:yyyy-MM-dd HH:mm})");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ModuleProps DeserializeProps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ModuleProps();

        try
        {
            return JsonSerializer.Deserialize<ModuleProps>(json) ?? new ModuleProps();
        }
        catch
        {
            return new ModuleProps();
        }
    }

    private static string LoadEvolutionPrompt()
    {
        var searchPaths = new[]
        {
            "Data/SIGNALS_EVOLUTION_PROMPT.txt",
            "../BioChain.Repository/Data/SIGNALS_EVOLUTION_PROMPT.txt",
        };

        foreach (var path in searchPaths)
        {
            var full = Path.GetFullPath(path, AppContext.BaseDirectory);
            if (File.Exists(full))
                return File.ReadAllText(full);
        }

        return "You are a signal graph evolution agent. Analyze the graph and output Signals Kernel DSL predictions and updates.";
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
