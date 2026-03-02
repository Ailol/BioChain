# Agent Ecosystem — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build an evolving agent ecosystem where MODULEs live in the signal graph, react to changes via PG LISTEN/NOTIFY, test predictions against reality, and evolve the model over time — all through a single new BackgroundService that reuses existing infrastructure.

**Architecture:** One new `AgentEcosystemService : BackgroundService` that listens for graph changes (same pattern as `GraphSyncService`), evaluates active MODULE gates, extracts Neo4j subgraphs, sends evolution context to the LLM, and parses output back through the existing `BioChainParser` → `AnalyzeService.LinkComponentAsync` pipeline. Zero new database tables.

**Tech Stack:** C# / .NET 9+ / EF Core / Npgsql / Neo4j.Driver / Microsoft.Extensions.AI / PostgreSQL LISTEN/NOTIFY

---

## Project Structure & Dependency Chain

```
BioChain.Server (Web SDK — entry point, DI, API endpoints)
  → BioChain.Service (class library — BackgroundServices, business logic)
      → BioChain.Repository (class library — EF Core entities, DbContext, repos, prompt .txt files, SQL)
      → BioChain.AgentFramework (class library — ChatClient, IChatClient wrapping, Microsoft.Extensions.AI)
      → BioChain.AnalysisFramework (class library — pure computation, algorithms)
      → BioChain.Utils (class library — BioChainParser, document extraction)
      → BioChain.Models (class library — shared DTOs)
      → BioChain.ML (class library — ML models)
  → BioChain.Repository (direct ref for DI registration)
  → BioChain.AgentFramework (direct ref for DI registration)
  → BioChain.Utils (direct ref)
```

**Key rules for file placement:**
- **Entities, repositories, DbContext, prompt files, SQL** → `BioChain.Repository`
- **BackgroundServices, business logic, service-layer models** → `BioChain.Service`
- **LLM wrappers, agent orchestration** → `BioChain.AgentFramework`
- **Parsing, document extraction** → `BioChain.Utils`
- **DI wiring, API endpoints, Program.cs** → `BioChain.Server`
- **Prompt .txt files** → `BioChain.Repository/Data/` with `<Content CopyToOutputDirectory="PreserveNewest" />` in `.csproj`

**Package availability (transitive):**
- `BioChain.Service` gets `Npgsql` transitively from Repository, `IChatClient` from AgentFramework, `Neo4j.Driver` directly
- `BioChain.Service` has `Microsoft.Extensions.Hosting.Abstractions` for `BackgroundService` and `IServiceScopeFactory`

---

## Reference Files

Before starting, familiarize yourself with:

| File | Why |
|------|-----|
| `src/Libraries/BioChain.Service/GraphSyncService.cs` | Pattern to copy: LISTEN/NOTIFY, debounce, BackgroundService |
| `src/Libraries/BioChain.Service/BioChain.Service.csproj` | Package refs + project refs for this layer |
| `src/Libraries/BioChain.Repository/BioChain.Repository.csproj` | Content includes for prompt files |
| `src/Libraries/BioChain.Repository/Repositories/IModuleRepository.cs` | Current interface — needs UpdatePropertiesAsync |
| `src/Libraries/BioChain.Repository/Repositories/ModuleRepository.cs` | EF Core implementation to extend |
| `src/Libraries/BioChain.Repository/Entities/ModuleEntity.cs` | Module entity with Properties JSONB |
| `src/Libraries/BioChain.Repository/Entities/ProtocolEntity.cs` | Protocol entity — predictions stored here |
| `src/Libraries/BioChain.Repository/Repositories/IProtocolRepository.cs` | Needs GetByModuleTagAsync |
| `src/Frameworks/BioChain.AgentFramework/ChatClient.cs` | LLM wrapper: `SendAsync(system, user, ct)` |
| `src/Libraries/BioChain.Service/AnalyzeService.cs` | `LinkComponentAsync` — entity creation pipeline |
| `src/Libraries/BioChain.Service/Neo4jGraphStore.cs` | Neo4j sync — `IGraphStore` interface |
| `src/Libraries/BioChain.Utils/Parsing/BioChainParser.cs` | Parser — already handles PREDICTION tag |
| `src/Libraries/BioChain.Repository/Data/biochain_graph.sql` | `evaluate_gate()`, `serialize_profile_dsl()`, `export_graph_json()` |
| `src/BioChain.Server/Program.cs` | DI registrations — RegisterAll() |

---

## Phase 1: Repository Extensions

### Task 1: Add UpdatePropertiesAsync to IModuleRepository

The agent ecosystem needs to update module lifecycle properties (eval_count, hit_count, utility, last_eval) after each evaluation cycle. The current repository has no update method.

**Files:**
- Modify: `src/Libraries/BioChain.Repository/Repositories/IModuleRepository.cs`

Add one new method to the interface:

```csharp
Task UpdatePropertiesAsync(int moduleId, string propertiesJson, CancellationToken ct = default);
```

The full interface after modification:

```csharp
using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IModuleRepository
{
    Task<ModuleEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<ModuleEntity>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<ModuleEntity?> GetCurrentByCodeAsync(Guid subjectId, string code, CancellationToken ct = default);
    Task<List<ModuleEntity>> GetByAgentTypeAsync(Guid subjectId, string agentType, CancellationToken ct = default);
    Task<List<ModuleEntity>> GetByNamespaceAsync(Guid subjectId, string ns, CancellationToken ct = default);
    Task<ModuleEntity> CreateAsync(ModuleEntity entity, CancellationToken ct = default);
    Task UpdatePropertiesAsync(int moduleId, string propertiesJson, CancellationToken ct = default);
}
```

### Task 2: Implement UpdatePropertiesAsync in ModuleRepository

**Files:**
- Modify: `src/Libraries/BioChain.Repository/Repositories/ModuleRepository.cs`

Add the implementation after the existing `CreateAsync` method:

```csharp
public async Task UpdatePropertiesAsync(int moduleId, string propertiesJson, CancellationToken ct = default)
{
    var entity = await db.Modules.FindAsync([moduleId], ct)
        ?? throw new InvalidOperationException($"Module {moduleId} not found");
    entity.Properties = propertiesJson;
    await db.SaveChangesAsync(ct);
}
```

### Task 3: Add GetByModuleTagAsync to IProtocolRepository

The ecosystem service needs to load prediction history for a specific module. Current interface only has `GetByPersonAsync` (all protocols for a subject).

**Files:**
- Modify: `src/Libraries/BioChain.Repository/Repositories/IProtocolRepository.cs`

Add one new method:

```csharp
Task<List<ProtocolEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default);
```

Full interface after:

```csharp
using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IProtocolRepository
{
    Task<ProtocolEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<ProtocolEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<ProtocolEntity>> GetGlobalAsync(CancellationToken ct = default);
    Task<ProtocolEntity> CreateAsync(ProtocolEntity entity, CancellationToken ct = default);
    Task<List<ProtocolEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default);
}
```

### Task 4: Implement GetByModuleTagAsync in ProtocolRepository

**Files:**
- Modify: `src/Libraries/BioChain.Repository/Repositories/ProtocolRepository.cs`

Add the implementation:

```csharp
public Task<List<ProtocolEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default)
    => db.Protocols
        .Where(p => p.ModuleId == moduleId && p.Tag == tag)
        .OrderByDescending(p => p.CreatedOnUtc)
        .ToListAsync(ct);
```

### Task 5: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors, 0 warnings. The new interface methods must match their implementations.

### Task 6: Commit

```bash
git add src/Libraries/BioChain.Repository/Repositories/IModuleRepository.cs src/Libraries/BioChain.Repository/Repositories/ModuleRepository.cs src/Libraries/BioChain.Repository/Repositories/IProtocolRepository.cs src/Libraries/BioChain.Repository/Repositories/ProtocolRepository.cs
git commit -m "feat: add UpdatePropertiesAsync and GetByModuleTagAsync to repositories"
```

---

## Phase 2: Module Lifecycle Properties Model

### Task 7: Create ModuleProps helper class

This is a strongly-typed C# model for the `module.properties` JSONB field. Keeps lifecycle state structured instead of raw JSON manipulation.

**Files:**
- Create: `src/Libraries/BioChain.Service/Models/ModuleProps.cs`

```csharp
using System.Text.Json.Serialization;

namespace BioChain.Service.Models;

/// <summary>
/// Strongly-typed view of module.properties JSONB.
/// Tracks lifecycle: status, utility, generation, evaluation stats, watch lists.
/// </summary>
public sealed class ModuleProps
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("utility")]
    public double Utility { get; set; } = 0.5;

    [JsonPropertyName("generation")]
    public int Generation { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }

    [JsonPropertyName("hit_count")]
    public int HitCount { get; set; }

    [JsonPropertyName("last_eval")]
    public DateTimeOffset? LastEval { get; set; }

    [JsonPropertyName("watch_signals")]
    public string[] WatchSignals { get; set; } = [];

    [JsonPropertyName("watch_constraints")]
    public int[] WatchConstraints { get; set; } = [];

    /// <summary>Passthrough for any extra keys (def, import, etc.) from MODULE creation.</summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Extra { get; set; }
}
```

### Task 8: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors. The new class is standalone — no dependencies to break.

### Task 9: Commit

```bash
git add src/Libraries/BioChain.Service/Models/ModuleProps.cs
git commit -m "feat: add ModuleProps model for module lifecycle JSONB"
```

---

## Phase 3: PREDICTION Tag Support

The parser already recognizes `PREDICTION` as a valid tag (it's in the `Tags` HashSet). AnalyzeService needs a handler to store predictions as protocols with module linkage.

### Task 10: Verify PREDICTION is in BioChainParser.Tags

**Run:** Search for `PREDICTION` in `src/Libraries/BioChain.Utils/Parsing/BioChainParser.cs`

Expected: `"PREDICTION"` is already in the `Tags` HashSet. If NOT, add it.

### Task 11: Add PREDICTION handler in AnalyzeService.LinkComponentAsync

**Files:**
- Modify: `src/Libraries/BioChain.Service/AnalyzeService.cs`

Find the `LinkComponentAsync` method's switch statement. Add a case for `"PREDICTION"` near the existing `"HYPOTHESIS"` case. Predictions are stored as protocol entries (same as HYPOTHESIS/INTERVENTION) so the LLM evolution loop can query them later.

The handler should be very simple — predictions are just protocols with tag=PREDICTION:

```csharp
case "PREDICTION":
{
    // Predictions stored as protocols — evolution loop queries them via GetByModuleTagAsync
    // The formula contains the prediction text, status tracks resolution (pending/confirmed/refuted)
    var predProtocol = new ProtocolEntity
    {
        SubjectId = subjectId,
        StimuliId = protocol.Id,
        Tag = "PREDICTION",
        Formula = line.Formula,
        Status = line.Status ?? "pending",
        Phase = line.Phase,
        Seq = line.Seq,
    };
    await protocols.CreateAsync(predProtocol, ct);
    break;
}
```

**Note:** Check if there's already a handler that catches PREDICTION (e.g., a default case that creates a protocol). If so, just ensure it sets `Tag = "PREDICTION"` and `Status = "pending"`.

### Task 12: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors.

### Task 13: Commit

```bash
git add src/Libraries/BioChain.Service/AnalyzeService.cs
git commit -m "feat: add PREDICTION tag handler in AnalyzeService"
```

---

## Phase 4: Evolution Prompt

### Task 14: Create SIGNALS_EVOLUTION_PROMPT.txt + add Content include to csproj

This is the system prompt sent to the LLM when a MODULE's gate fires. It receives the module's context (definition, subgraph, prediction history) and outputs Signals Kernel DSL that goes through the existing parser pipeline.

**Files:**
- Create: `src/Libraries/BioChain.Repository/Data/SIGNALS_EVOLUTION_PROMPT.txt`
- Modify: `src/Libraries/BioChain.Repository/BioChain.Repository.csproj` — add Content include

**Important:** Prompt `.txt` files live in `BioChain.Repository/Data/` (the data layer) but must have `<Content CopyToOutputDirectory="PreserveNewest" />` entries in the `.csproj` so they're copied to the output directory at build time. The service layer (`BioChain.Service`) loads them at runtime via `Path.GetFullPath("Data/...", AppContext.BaseDirectory)`.

Add to the existing `<ItemGroup>` in `BioChain.Repository.csproj` that already has the BIOCHAIN_ANALYZER_PROMPT:

```xml
<Content Include="Data\SIGNALS_EVOLUTION_PROMPT.txt" CopyToOutputDirectory="PreserveNewest" />
<Content Include="Data\SIGNALS_ANALYZER_PROMPT.txt" CopyToOutputDirectory="PreserveNewest" />
<Content Include="Data\SIGNALS_AGENT_SPAWNER_PROMPT.txt" CopyToOutputDirectory="PreserveNewest" />
```

(The existing v1.5 migration created SIGNALS_ANALYZER_PROMPT.txt and SIGNALS_AGENT_SPAWNER_PROMPT.txt but missed adding their Content includes — fix them here too.)

```text
You are a Signals Kernel evolution agent. You analyze signal graphs, make predictions, and evolve the model.

## Context

You will receive:
1. A MODULE definition with its current lifecycle properties (utility, eval_count, hit_count, generation)
2. A subgraph in Signals Kernel DSL showing the signals, edges, and constraints this module watches
3. Prediction history — previous predictions you made and whether they were confirmed or refuted
4. Active constraints and their current status

## Your Task

Analyze the current state of the watched signals and:

1. **Evaluate predictions**: Compare previous predictions against current signal states. For each resolved prediction, output:
   ```
   PREDICTION: [original prediction] — status: confirmed
   PREDICTION: [original prediction] — status: refuted
   ```

2. **Make new predictions**: Based on edge gains, feedback loops, and signal trends, predict what will happen:
   ```
   PREDICTION: [SIGNAL.REGION] → [state] within [timeframe] based on [reasoning]
   PREDICTION: [SIGNAL/SIGNAL.REGION] will breach CONSTRAINT:[id] ([expression]) if [trend] continues
   ```

3. **Update signals** if you observe state changes the graph hasn't captured:
   ```
   SIGNAL: TYPE:CODE.REGION STATE — status: [reasoning]
   ```

4. **Adjust constraints** if boundaries need tightening or relaxing:
   ```
   CONSTRAINT: [expression] — status: [reasoning]
   ```

5. **Spawn sub-MODULEs** if you detect patterns that need dedicated monitoring:
   ```
   MODULE: [name] {
     DEF: [what it monitors]
     AGENT: REACTIVE
     IMPORT: [signals to watch]
   }
   ```

6. **Update formulas** if edge gains or feedback loops need recalibration:
   ```
   FORMULA: SOURCE → TARGET [operator] gain=[value] delay=[ms]
   ```

## Output Rules

- Output ONLY valid Signals Kernel DSL tags (PREDICTION, SIGNAL, CONSTRAINT, MODULE, FORMULA, FEEDBACK, etc.)
- One tag per line (except MODULE blocks which use { } braces)
- Be conservative — only output changes you're confident about
- Predictions should be falsifiable and time-bounded
- Use symbolic states (↓↓, ↓, ≈, ↑, ↑↑) when no numeric values are available
- Use numeric values when available: `SIGNAL: N:CORT.SERUM ↑↑ value=34 unit=ug/dL baseline=10`
- If nothing significant has changed, output a single line: `-- no changes`
- Do NOT output explanatory text — only DSL tags

## State Symbol Reference

States: ↓↓ (strongly decreased) < ↓ (decreased) < ≈ (baseline) < ↑ (elevated) < ↑↑ (strongly elevated)
Trends: ↗ (rising), ↘ (falling), → (stable), ~ (oscillating)
Feedback: ⟳⁺ (positive/amplifying), ⟳⁻ (negative/dampening)
```

### Task 15: Commit

```bash
git add src/Libraries/BioChain.Repository/Data/SIGNALS_EVOLUTION_PROMPT.txt src/Libraries/BioChain.Repository/BioChain.Repository.csproj
git commit -m "feat: add evolution prompt + fix Content includes for prompt files"
```

---

## Phase 5: AgentEcosystemService — Core

### Task 16: Create AgentEcosystemService skeleton

This is the main BackgroundService. It follows the exact same LISTEN/NOTIFY + debounce pattern as `GraphSyncService`, but instead of syncing to Neo4j, it evaluates MODULE gates and triggers LLM evolution cycles.

**Files:**
- Create: `src/Libraries/BioChain.Service/AgentEcosystemService.cs`

**Dependencies (all existing, injected via DI):**
- `IServiceScopeFactory` — create scoped DbContext per evaluation (BackgroundService is singleton, repos are scoped)
- `IConfiguration` — PG connection string, debounce config
- `ILogger<AgentEcosystemService>`
- `IDriver` (Neo4j) — Cypher queries for subgraph extraction (optional — only if Neo4j configured)
- `IChatClient` — LLM calls for evolution

**Important DI note:** Since repositories are scoped and BackgroundService is singleton, create a new `IServiceScope` for each evaluation cycle, resolve repos from the scope, and dispose after.

```csharp
using System.Collections.Concurrent;
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
///         → extract 2-hop subgraph (Neo4j Cypher or PG DSL)
///         → load prediction history
///         → call LLM with evolution prompt
///         → parse output → LinkComponentAsync → write back to graph
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
            var table = root.TryGetProperty("table", out var t) ? t.GetString() : null;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;

            if (code is not null)
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
                    var overlap = props.WatchSignals.Intersect(changedCodes, StringComparer.OrdinalIgnoreCase).Any();
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
                var llmOutput = await _engine.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, EvolutionPrompt),
                    new ChatMessage(ChatRole.User, userContext),
                ]);
                var outputText = llmOutput.Text ?? "";

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

                    // Create a dummy protocol to anchor parsed lines
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
                        await analyzeService.LinkComponentPublicAsync(anchorProtocol, line, subjectId, default);
                    }
                }

                // 9. Update module lifecycle
                var predictionLines = lines.Where(l => l.Tag == "PREDICTION").ToList();
                var confirmedCount = predictionLines.Count(l =>
                    l.Status?.Contains("confirmed", StringComparison.OrdinalIgnoreCase) == true);

                props.EvalCount++;
                props.HitCount += confirmedCount;
                props.Utility = props.EvalCount > 0 ? (double)props.HitCount / props.EvalCount : 0.5;
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
    }

    // ── Gate Evaluation via PG ───────────────────────────────────────────────

    private async Task<bool> EvaluateModuleGateAsync(ModuleEntity module, Guid subjectId, IServiceScope scope)
    {
        // Find the gate associated with this module (if any)
        var gateRepo = scope.ServiceProvider.GetRequiredService<IGateRepository>();
        var moduleGates = await gateRepo.GetBySubjectAsync(subjectId);
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

    private async Task<string> ExtractSubgraphAsync(Guid subjectId, string[] watchSignals, IServiceScope scope)
    {
        // Strategy: Use PG serialize_profile_dsl() — it's fast and already produces LLM-ready DSL
        // Neo4j Cypher extraction is an optimization for later (targeted 2-hop neighborhoods)
        await using var conn = new NpgsqlConnection(_pgConnString);
        await conn.OpenAsync();

        // Refresh materialized view first
        await using (var refreshCmd = new NpgsqlCommand("SELECT refresh_graph()", conn))
            await refreshCmd.ExecuteNonQueryAsync();

        await using var cmd = new NpgsqlCommand("SELECT serialize_profile_dsl($1)", conn);
        cmd.Parameters.AddWithValue(subjectId);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "(empty graph)";
    }

    // ── Build LLM Context ────────────────────────────────────────────────────

    private static string BuildEvolutionContext(
        ModuleEntity module,
        ModuleProps props,
        string subgraphDsl,
        List<ProtocolEntity> predictions)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## MODULE");
        sb.AppendLine($"Code: {module.Code}");
        sb.AppendLine($"Agent Type: {module.AgentType ?? "REACTIVE"}");
        sb.AppendLine($"Generation: {props.Generation}");
        sb.AppendLine($"Utility: {props.Utility:F2} ({props.HitCount}/{props.EvalCount} hits)");
        sb.AppendLine($"Watch Signals: {string.Join(", ", props.WatchSignals)}");
        sb.AppendLine();

        sb.AppendLine("## CURRENT GRAPH STATE");
        sb.AppendLine(subgraphDsl);
        sb.AppendLine();

        if (predictions.Count > 0)
        {
            sb.AppendLine("## PREDICTION HISTORY");
            foreach (var p in predictions.Take(20)) // Limit context window
            {
                sb.AppendLine($"- [{p.Status ?? "pending"}] {p.Formula} (created: {p.CreatedOnUtc:yyyy-MM-dd HH:mm})");
            }
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
```

### Task 17: Expose LinkComponentAsync publicly on AnalyzeService

The `AgentEcosystemService` needs to call `LinkComponentAsync` to route parsed LLM output into entity creation. Currently this method is private.

**Files:**
- Modify: `src/Libraries/BioChain.Service/AnalyzeService.cs`

Add a thin public wrapper that delegates to the private method. Do NOT change the private method signature — just add one public method:

```csharp
/// <summary>
/// Public entry point for the agent ecosystem to route parsed DSL lines
/// through the same entity creation pipeline used by initial analysis.
/// </summary>
public Task LinkComponentPublicAsync(
    ProtocolEntity protocol, BioChainLine line, Guid subjectId, CancellationToken ct)
    => LinkComponentAsync(protocol, line, subjectId, ct);
```

Place this right before the private `LinkComponentAsync` method.

### Task 18: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors. If there are issues with `IChatClient.GetResponseAsync` vs `ChatClient.SendAsync`, adjust the LLM call in `EvaluateModulesAsync` to use the correct API.

### Task 19: Commit

```bash
git add src/Libraries/BioChain.Service/AgentEcosystemService.cs src/Libraries/BioChain.Service/AnalyzeService.cs
git commit -m "feat: add AgentEcosystemService core with LISTEN/debounce/evaluate loop"
```

---

## Phase 6: DI Registration + Wiring

### Task 20: Register AgentEcosystemService in Program.cs

**Files:**
- Modify: `src/BioChain.Server/Program.cs`

In the `RegisterAll` method, add the agent ecosystem service registration inside the existing Neo4j conditional block (it depends on Neo4j for future subgraph queries, but works without it using PG DSL fallback).

Find this section (around line 155-164):

```csharp
// Neo4j graph sync (optional — only if Neo4j:Uri is configured)
var neo4jUri = appConfig["Neo4j:Uri"];
if (!string.IsNullOrEmpty(neo4jUri))
{
    // ... existing Neo4j registration ...
    services.AddHostedService<GraphSyncService>();
}
```

Add the agent ecosystem registration AFTER the Neo4j block (it works with or without Neo4j):

```csharp
// Agent ecosystem (optional — only if an analysis LLM is configured)
if (llm.AgentAnalyzing is not null)
{
    services.AddHostedService<AgentEcosystemService>();
}
```

**Note:** The `AgentEcosystemService` constructor takes `IChatClient` which is already registered as a singleton from `llm.AgentAnalyzing`. The `IServiceScopeFactory` is automatically available in DI.

### Task 21: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors. The service should resolve all its constructor dependencies.

### Task 22: Commit

```bash
git add src/BioChain.Server/Program.cs
git commit -m "feat: register AgentEcosystemService in DI"
```

---

## Phase 7: Neo4j Cypher Pattern Detection (Optional Enhancement)

This phase adds targeted subgraph extraction via Neo4j Cypher queries. It's an optimization over the PG DSL fallback — provides focused context (2-hop neighborhood of watched signals) instead of the full profile DSL.

### Task 23: Add Cypher subgraph extraction method

**Files:**
- Modify: `src/Libraries/BioChain.Service/AgentEcosystemService.cs`

Add a Neo4j-based extraction method that's used when Neo4j is available. Replace the `ExtractSubgraphAsync` method with a version that tries Neo4j first, falls back to PG DSL:

```csharp
private async Task<string> ExtractSubgraphAsync(Guid subjectId, string[] watchSignals, IServiceScope scope)
{
    // Try Neo4j targeted extraction if available and we have specific signals to watch
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
            state: node.state,
            properties: node.properties
        }) AS nodes,
        [rel IN allRels WHERE rel IS NOT NULL |
            {
                source: startNode(rel).code,
                target: endNode(rel).code,
                operator: type(rel),
                properties: rel.properties
            }
        ] AS edges
        """;

    await using var session = driver.AsyncSession();
    var result = await session.RunAsync(query, new
    {
        pid = subjectId.ToString(),
        codes = signalCodes
    });

    var record = await result.SingleAsync();
    var nodes = record["nodes"].As<List<object>>();
    var edges = record["edges"].As<List<object>>();

    // Format as DSL-like text for LLM context
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("# Subgraph (2-hop neighborhood)");
    sb.AppendLine();

    foreach (var node in nodes)
    {
        if (node is IDictionary<string, object> n)
            sb.AppendLine($"  {n.GetValueOrDefault("kind")}:{n.GetValueOrDefault("code")} [{n.GetValueOrDefault("state") ?? "≈"}]");
    }
    sb.AppendLine();

    foreach (var edge in edges)
    {
        if (edge is IDictionary<string, object> e)
            sb.AppendLine($"  {e.GetValueOrDefault("source")} → {e.GetValueOrDefault("target")} [{e.GetValueOrDefault("operator")}]");
    }

    return sb.ToString();
}
```

### Task 24: Add pattern detection Cypher queries

Add a method that detects structural patterns needing new MODULEs. This enables automatic spawning of agents for unmonitored feedback loops, cascades, etc.

**Files:**
- Modify: `src/Libraries/BioChain.Service/AgentEcosystemService.cs`

Add after the extraction methods:

```csharp
/// <summary>
/// Detect structural patterns in the graph that might benefit from dedicated MODULE monitoring.
/// Returns pattern descriptions that can seed MODULE spawning.
/// </summary>
public async Task<List<string>> DetectPatternsAsync(Guid subjectId, IServiceScope scope)
{
    if (!_hasNeo4j) return [];

    var driver = scope.ServiceProvider.GetService<Neo4j.Driver.IDriver>();
    if (driver is null) return [];

    var patterns = new List<string>();
    await using var session = driver.AsyncSession();

    // 1. Positive feedback loops with elevated signals
    try
    {
        var feedbackQuery = """
            MATCH (a)-[r:FEEDBACK]->(b)
            WHERE a.subject_id = $pid
              AND a.state IN ['↑', '↑↑']
              AND r.properties CONTAINS '⟳⁺'
            RETURN a.code AS source, b.code AS target, a.state AS state
            """;

        var result = await session.RunAsync(feedbackQuery, new { pid = subjectId.ToString() });
        var records = await result.ToListAsync();
        foreach (var r in records)
        {
            patterns.Add($"FEEDBACK_LOOP: {r["source"]}->{r["target"]} ({r["state"]}) — positive feedback with elevated signal");
        }
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "[AgentEcosystem] Feedback loop detection query failed");
    }

    // 2. Long cascade paths (depth >= 3)
    try
    {
        var cascadeQuery = """
            MATCH path = (a)-[*3..5]->(b)
            WHERE a.subject_id = $pid
              AND a.state IN ['↑↑', '↓↓']
            RETURN [n IN nodes(path) | n.code] AS cascade, length(path) AS depth
            LIMIT 5
            """;

        var result = await session.RunAsync(cascadeQuery, new { pid = subjectId.ToString() });
        var records = await result.ToListAsync();
        foreach (var r in records)
        {
            var cascade = string.Join(" → ", r["cascade"].As<List<string>>());
            patterns.Add($"CASCADE: {cascade} (depth={r["depth"]}) — unmonitored multi-hop cascade");
        }
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "[AgentEcosystem] Cascade detection query failed");
    }

    return patterns;
}
```

### Task 25: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors. The Neo4j code uses `IDriver` which is already referenced in the project.

### Task 26: Commit

```bash
git add src/Libraries/BioChain.Service/AgentEcosystemService.cs
git commit -m "feat: add Neo4j Cypher subgraph extraction and pattern detection"
```

---

## Phase 8: Module Spawning

### Task 27: Add automatic MODULE spawning from detected patterns

When `DetectPatternsAsync` finds unmonitored patterns (feedback loops, cascades), the ecosystem should automatically create new MODULEs to watch them.

**Files:**
- Modify: `src/Libraries/BioChain.Service/AgentEcosystemService.cs`

Add a method and integrate it into the evaluation loop:

```csharp
/// <summary>
/// Spawn new MODULEs for detected patterns that don't have existing coverage.
/// Called periodically (e.g., every 10th evaluation cycle) to grow the ecosystem.
/// </summary>
private async Task SpawnModulesForPatternsAsync(Guid subjectId, IServiceScope scope)
{
    var moduleRepo = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
    var existingModules = await moduleRepo.GetBySubjectAsync(subjectId);

    var patterns = await DetectPatternsAsync(subjectId, scope);
    if (patterns.Count == 0) return;

    foreach (var pattern in patterns)
    {
        // Extract signal codes from pattern description
        var signalCodes = ExtractSignalCodesFromPattern(pattern);

        // Check if any existing module already watches these signals
        var alreadyCovered = existingModules.Any(m =>
        {
            var p = DeserializeProps(m.Properties);
            return p.Status == "active" &&
                   signalCodes.All(c => p.WatchSignals.Contains(c, StringComparer.OrdinalIgnoreCase));
        });

        if (alreadyCovered) continue;

        // Determine agent type from pattern
        var agentType = pattern.StartsWith("FEEDBACK_LOOP") ? "MONITOR" : "DIAGNOSER";
        var code = $"auto_{agentType.ToLower()}_{signalCodes.FirstOrDefault() ?? "unknown"}";

        // Truncate code to 50 chars
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
    // Extract signal codes like "CORT", "GLU", "GABA" from pattern descriptions
    // Pattern format: "TYPE: SOURCE->TARGET (state) — description"
    var codes = new List<string>();
    var parts = pattern.Split([':', '→', '-', '>', '(', ')', ' '], StringSplitOptions.RemoveEmptyEntries);
    foreach (var part in parts)
    {
        var trimmed = part.Trim();
        // Signal codes are typically 2-10 uppercase letters (possibly with dots for region)
        if (trimmed.Length >= 2 && trimmed.Length <= 30 &&
            trimmed.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_') &&
            trimmed.Any(char.IsUpper) &&
            !new[] { "FEEDBACK", "LOOP", "CASCADE", "MONITOR", "DIAGNOSER" }
                .Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            codes.Add(trimmed);
        }
    }
    return codes.Distinct().Take(5).ToArray();
}
```

In the `EvaluateModulesAsync` method, add a periodic spawning check at the end (after the module evaluation loop):

```csharp
// After the foreach (var module in allModules) loop ends:

// Periodically check for new patterns to spawn MODULEs
// (every evaluation, since the debounce already rate-limits)
try
{
    await SpawnModulesForPatternsAsync(subjectId, scope);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "[AgentEcosystem] Pattern spawning failed for subject {SubjectId}", subjectId);
}
```

### Task 28: Build and verify

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors.

### Task 29: Commit

```bash
git add src/Libraries/BioChain.Service/AgentEcosystemService.cs
git commit -m "feat: add automatic MODULE spawning from detected graph patterns"
```

---

## Phase 9: Build & Integration Test

### Task 30: Full build

**Run:** `dotnet build src/BioChain.Server/BioChain.Server.csproj`

Expected: 0 errors, 0 warnings.

### Task 31: Manual integration test

Start the server and verify the agent ecosystem starts:

```bash
cd src/BioChain.Server
ENVIRONMENT=Development dotnet run
```

Expected log output should include:
```
[AgentEcosystem] Starting — debounce=2000ms, neo4j=True
[AgentEcosystem] LISTEN connected
```

Then trigger an analysis to create graph data:

```bash
curl -s -X POST http://localhost:5000/api/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "subjectId": "<entity-guid>",
    "text": "Patient shows elevated cortisol with positive feedback to glutamate. GABA levels declining.",
    "kind": "observation"
  }'
```

The agent ecosystem should:
1. Receive the `graph_changed` notification
2. Debounce for 2 seconds
3. Load any active MODULEs for the subject
4. If MODULEs exist with matching watch signals → evaluate gates → call LLM → parse output
5. If no MODULEs exist but patterns are detected → spawn new MODULEs

### Task 32: Final commit

```bash
git add -A
git commit -m "feat: complete agent ecosystem implementation

- Add UpdatePropertiesAsync + GetByModuleTagAsync to repositories
- Add ModuleProps lifecycle model for JSONB properties
- Add PREDICTION tag handler in AnalyzeService
- Create SIGNALS_EVOLUTION_PROMPT.txt for LLM evolution calls
- Implement AgentEcosystemService BackgroundService
  - LISTEN/NOTIFY + debounce (same pattern as GraphSyncService)
  - MODULE gate evaluation via PG evaluate_gate()
  - Subgraph extraction via PG serialize_profile_dsl() + Neo4j Cypher
  - LLM evolution → BioChainParser → LinkComponentAsync pipeline
  - Module lifecycle tracking (utility, eval/hit counts, auto-retirement)
- Add Neo4j Cypher pattern detection (feedback loops, cascades)
- Add automatic MODULE spawning for unmonitored patterns
- Register AgentEcosystemService in DI"
```

---

## Summary

| Phase | What | Files | Project |
|-------|------|-------|---------|
| 1 | Repository extensions (UpdateProperties, GetByModuleTag) | 4 modified | `BioChain.Repository` |
| 2 | ModuleProps lifecycle model | 1 created | `BioChain.Service` |
| 3 | PREDICTION tag handler | 1 modified | `BioChain.Service` |
| 4 | Evolution prompt + csproj Content includes | 1 created + 1 modified | `BioChain.Repository` |
| 5 | AgentEcosystemService core | 1 created + 1 modified | `BioChain.Service` |
| 6 | DI registration | 1 modified | `BioChain.Server` |
| 7 | Neo4j Cypher extraction + patterns | 1 modified | `BioChain.Service` |
| 8 | Module spawning | 1 modified | `BioChain.Service` |
| 9 | Build + integration test | 0 | All |

**Total: ~400-500 lines of new C# code + 1 prompt file + 1 csproj update**

**Reuses:** GraphSyncService pattern, evaluate_gate(), BioChainParser, AnalyzeService.LinkComponentAsync, serialize_profile_dsl(), IChatClient, all existing repositories.
