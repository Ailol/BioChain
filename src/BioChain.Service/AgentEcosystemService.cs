using System.Text;
using System.Text.Json;
using BioChain.Kernel.Agents;
using BioChain.Kernel.Prompts;
using BioChain.Repository.Entities;
using BioChain.Repository.Linking;
using BioChain.Repository.Listeners;
using BioChain.Repository.Repositories;
using BioChain.Service.Models;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BioChain.Service;

/// <summary>
/// Evolving agent ecosystem. Delegates LISTEN/debounce to <see cref="IGraphChangeListener"/>,
/// PG function calls to <see cref="IGraphQueryRepository"/>, LLM evolution to <see cref="ILlmEngine"/>,
/// and DSL-to-entity linking to <see cref="IComponentLinker"/>.
/// Keeps Neo4j pattern detection (Service-only dependency).
/// </summary>
public sealed class AgentEcosystemService : BackgroundService
{
    private readonly IGraphChangeListener _listener;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILlmEngine _engine;
    private readonly string _evolutionPrompt;
    private readonly bool _hasNeo4j;
    private readonly ILogger<AgentEcosystemService> _logger;

    public AgentEcosystemService(
        IGraphChangeListener listener,
        IServiceScopeFactory scopeFactory,
        ILlmEngine engine,
        IPromptStore prompts,
        IConfiguration config,
        ILogger<AgentEcosystemService> logger)
    {
        _listener = listener;
        _scopeFactory = scopeFactory;
        _engine = engine;
        _evolutionPrompt = prompts.LoadOrDefault(
            "SIGNALS_EVOLUTION_PROMPT.txt",
            "You are a signal graph evolution agent. Analyze the graph and output Signals Kernel DSL predictions and updates.");
        _hasNeo4j = !string.IsNullOrEmpty(config["Neo4j:Uri"]);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AgentEcosystem] Starting — neo4j={HasNeo4j}", _hasNeo4j);
        await _listener.ListenAsync(EvaluateModulesAsync, stoppingToken);
    }

    // ── Core Evaluation Loop ─────────────────────────────────────────────────

    private async Task EvaluateModulesAsync(Guid subjectId, string[] changedCodes)
    {
        using var scope = _scopeFactory.CreateScope();
        var moduleRepo = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var protocolRepo = scope.ServiceProvider.GetRequiredService<IProtocolRepository>();
        var gateRepo = scope.ServiceProvider.GetRequiredService<IGateRepository>();
        var graphQuery = scope.ServiceProvider.GetRequiredService<IGraphQueryRepository>();
        var linker = scope.ServiceProvider.GetRequiredService<IComponentLinker>();
        var stimuliRepo = scope.ServiceProvider.GetRequiredService<IStimuliRepository>();

        var allModules = await moduleRepo.GetBySubjectAsync(subjectId);

        foreach (var module in allModules)
        {
            try
            {
                var props = DeserializeProps(module.Properties);
                if (props.Status != "active") continue;

                if (!ShouldEvaluate(props, changedCodes)) continue;

                // Gate evaluation
                var gate = (await gateRepo.GetByPersonAsync(subjectId))
                    .FirstOrDefault(g => g.ModuleId == module.Id);
                if (gate is not null && !await graphQuery.EvaluateGateAsync(gate.Id, subjectId))
                    continue;

                // Extract subgraph
                var subgraphDsl = await ExtractSubgraphAsync(
                    subjectId, props.WatchSignals, scope, graphQuery);

                // Load prediction history → string[]
                var predictions = await protocolRepo.GetByModuleTagAsync(module.Id, "PREDICTION");
                var predFormulas = predictions
                    .Select(p => $"[{p.Status ?? "pending"}] {p.Formula} (created: {p.CreatedOnUtc:yyyy-MM-dd HH:mm})")
                    .ToArray();

                // Build evolution context and call LLM
                var userContext = BuildEvolutionContext(
                    module.Code, module.AgentType ?? "REACTIVE",
                    props.Generation, props.Utility, props.HitCount, props.EvalCount,
                    props.WatchSignals, subgraphDsl, predFormulas);
                var outputText = await _engine.ProcessAsync(_evolutionPrompt, userContext);

                _logger.LogDebug("[AgentEcosystem] Module {Code} LLM output: {Output}",
                    module.Code, outputText[..Math.Min(200, outputText.Length)]);

                // Skip if no changes
                if (outputText.Contains("-- no changes", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateLifecycle(props, 0);
                    await moduleRepo.UpdatePropertiesAsync(module.Id, JsonSerializer.Serialize(props));
                    continue;
                }

                // Parse LLM output and link via ComponentLinker
                var lines = BioChainParser.Parse(outputText);
                if (lines.Count > 0)
                {
                    var stimuliEntity = await stimuliRepo.CreateAsync(new StimuliEntity
                    {
                        SubjectId = subjectId,
                        Kind = "agent_evolution",
                        SourceText = $"MODULE:{module.Code} eval #{props.EvalCount + 1}",
                        Analyzed = true,
                    });

                    var anchorProtocol = await protocolRepo.CreateAsync(new ProtocolEntity
                    {
                        SubjectId = subjectId,
                        StimuliId = stimuliEntity.Id,
                        ModuleId = module.Id,
                        Tag = "EVOLUTION",
                        Formula = $"MODULE:{module.Code} generation={props.Generation}",
                    });

                    foreach (var line in lines)
                        await linker.LinkAsync(anchorProtocol, line, subjectId);
                }

                // Update lifecycle
                var confirmedCount = lines
                    .Where(l => l.Tag == "PREDICTION")
                    .Count(l => l.Status?.Contains("confirmed", StringComparison.OrdinalIgnoreCase) == true);

                UpdateLifecycle(props, confirmedCount);

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

        // Spawn new modules for detected patterns
        try
        {
            await SpawnModulesForPatternsAsync(subjectId, scope);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AgentEcosystem] Pattern spawning failed for subject {SubjectId}", subjectId);
        }
    }

    // ── Subgraph Extraction ──────────────────────────────────────────────────

    private async Task<string> ExtractSubgraphAsync(
        Guid subjectId, string[] watchSignals, IServiceScope scope,
        IGraphQueryRepository graphQuery)
    {
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

        return await graphQuery.SerializeProfileDslAsync(subjectId);
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
            var signalCodes = BioChainParser.ExtractSignalCodesFromPattern(pattern);

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

            await moduleRepo.CreateAsync(new ModuleEntity
            {
                SubjectId = subjectId,
                Code = code,
                AgentType = agentType,
                Properties = JsonSerializer.Serialize(new ModuleProps
                {
                    Status = "active",
                    Generation = 0,
                    WatchSignals = signalCodes,
                }),
            });

            _logger.LogInformation("[AgentEcosystem] Spawned MODULE {Code} for pattern: {Pattern}",
                code, pattern[..Math.Min(100, pattern.Length)]);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool ShouldEvaluate(ModuleProps props, string[] changedCodes)
    {
        if (props.WatchSignals.Length == 0) return true; // No filter = evaluate on any change
        if (changedCodes.Length == 0) return false;      // Has watch list but unknown changes
        return props.WatchSignals
            .Intersect(changedCodes, StringComparer.OrdinalIgnoreCase)
            .Any();
    }

    private static void UpdateLifecycle(ModuleProps props, int confirmedCount)
    {
        props.EvalCount++;
        props.HitCount += confirmedCount;
        props.Utility = props.EvalCount > 0 ? (double)props.HitCount / props.EvalCount : 0.5;
        props.LastEval = DateTimeOffset.UtcNow;
    }

    private static ModuleProps DeserializeProps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ModuleProps();
        try { return JsonSerializer.Deserialize<ModuleProps>(json) ?? new ModuleProps(); }
        catch { return new ModuleProps(); }
    }

    private static string BuildEvolutionContext(
        string moduleCode, string agentType, int generation,
        double utility, int hitCount, int evalCount,
        string[] watchSignals, string subgraphDsl,
        string[] predictionFormulas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MODULE");
        sb.AppendLine($"Code: {moduleCode}");
        sb.AppendLine($"Agent Type: {agentType}");
        sb.AppendLine($"Generation: {generation}");
        sb.AppendLine($"Utility: {utility:F2} ({hitCount}/{evalCount} hits)");
        if (watchSignals.Length > 0)
            sb.AppendLine($"Watch Signals: {string.Join(", ", watchSignals)}");
        sb.AppendLine();

        sb.AppendLine("## CURRENT GRAPH STATE");
        sb.AppendLine(subgraphDsl);
        sb.AppendLine();

        if (predictionFormulas.Length > 0)
        {
            sb.AppendLine("## PREDICTION HISTORY");
            foreach (var p in predictionFormulas.Take(20))
                sb.AppendLine($"- {p}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
