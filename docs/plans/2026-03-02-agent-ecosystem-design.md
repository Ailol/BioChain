# Agent Ecosystem Design — Evolving Signal Graph Agents

**Date:** 2026-03-02
**Status:** Approved for implementation

---

## Vision

Agents that **live in the signal graph, test formulas against reality, and evolve the model over time**. The graph is simultaneously the world model, the agent behavior definition, and the evolutionary substrate.

## Architecture: Hybrid Event-Triggered + LLM-Powered Evolution

### The Loop

```
Graph Change (PG NOTIFY)
    → AgentEcosystemService evaluates gates for living MODULEs
    → Gate fires → Neo4j extracts relevant subgraph (Cypher)
    → LLM gets: subgraph + formulas + prediction history
    → LLM outputs Signals Kernel DSL (predictions, evolution, new modules)
    → BioChainParser + AnalyzeService writes changes back to graph
    → GraphSyncService picks up changes → Neo4j updates
    → Cycle continues
```

### Agent Lifecycle

```
Generation 0:  Analysis → base graph (signals, edges, formulas, constraints)
Generation 1:  Pattern detection (Neo4j Cypher) → spawn initial MODULEs
Generation N:  New stimulus → graph changes → MODULEs react:
                 - Evaluate gates (fast, C#/PG)
                 - Make predictions (walk edges/formulas)
                 - Compare predictions vs reality
                 - Adapt: adjust gains, tighten gates, modify constraints
                 - Grow: spawn sub-MODULEs for new patterns
                 - Retire: self-deactivate when utility drops
```

### Five Natural Operations (emerge from graph structure)

| Operation | Trigger | How |
|-----------|---------|-----|
| **Monitor** | Constraint/boundary exists | Evaluate constraint against live signals |
| **Predict** | Signal trend + edge gain + boundary | Compute when boundary will be breached |
| **Diagnose** | Constraint violated | Trace backward through edges to root cause |
| **Simulate** | What-if query | Walk edges forward, apply gains, check constraints |
| **Intervene** | Diagnosis found root cause | Compute minimal upstream signal adjustment |

---

## Schema: Zero New Tables

All state stored in existing structures:

### Module Lifecycle → `module.properties` JSONB

```json
{
  "status": "active",
  "utility": 0.85,
  "generation": 2,
  "eval_count": 47,
  "hit_count": 40,
  "last_eval": "2026-03-02T12:00:00Z",
  "watch_signals": ["CORT", "GLU", "GABA"],
  "watch_constraints": [1, 3]
}
```

### Predictions → `protocol` table (existing)

Tag: `PREDICTION` — already supported by parser.

```
PREDICTION: CORT.ADR → ↑↑ within 48h based on ⟳⁺ feedback from GLU.PFC
PREDICTION: GLU/GABA.PFC will breach CONSTRAINT:1 (<=2.5) if GABA.PFC trend continues
```

Resolved predictions stored as follow-up protocols with accuracy metadata.

### Pattern Detection → Neo4j Cypher (no RAG table)

Neo4j IS the pattern matching engine. Cypher queries detect structural patterns:

```cypher
-- Positive feedback loops with elevated signals (spawn monitor)
MATCH (a)-[r:FEEDBACK]->(b)
WHERE a.state IN ['↑', '↑↑'] AND r.operator = '⟳⁺'
RETURN a.code AS source, b.code AS target, a.state

-- Signals approaching boundary (spawn predictor)
MATCH (s:signal)
WHERE s.state IN ['↑', '↑↑']
RETURN s.code, s.state
-- Cross-reference with constraint_def in PG for boundary values

-- Unmonitored cascade paths (spawn diagnoser)
MATCH path = (a)-[*2..4]->(b)
WHERE a.state IN ['↑↑', '↓↓']
RETURN [n IN nodes(path) | n.code] AS cascade, length(path) AS depth

-- High-centrality signals (important nodes needing MODULE coverage)
-- Use Neo4j GDS PageRank or betweenness centrality
```

### Behavior Templates → Agent Spawner Prompt

Static templates embedded in `SIGNALS_AGENT_SPAWNER_PROMPT.txt`. The LLM uses the template + current graph context to generate concrete MODULE definitions. No separate table needed — the prompt IS the pattern library. New templates = update the prompt file.

---

## Service Layer: One New BackgroundService

### `AgentEcosystemService` : BackgroundService

Plugs into existing infrastructure with minimal new code.

**Dependencies (all existing):**
- `NpgsqlConnection` — LISTEN graph_changed (same pattern as GraphSyncService)
- `IDriver` (Neo4j) — Cypher queries for subgraph extraction
- `LlmService` — evolution prompt LLM calls
- `IModuleRepository` — load/update MODULEs
- `IProtocolRepository` — load prediction history
- `BioChainParser` — parse LLM output
- `AnalyzeService.LinkComponentAsync()` — write entities back to graph

**Flow:**

```csharp
// 1. LISTEN for graph changes (same as GraphSyncService)
LISTEN graph_changed → OnNotification(entity_id, table, id)

// 2. Debounce per subject (reuse GraphSync pattern)
ScheduleEvaluation(subjectId)

// 3. Evaluate all active MODULEs for this subject
var modules = await moduleRepo.GetActiveBySubject(subjectId);
foreach (var module in modules)
{
    var props = JsonSerializer.Deserialize<ModuleProps>(module.Properties);

    // 4. Check if changed signals overlap with this module's watch list
    if (!props.WatchSignals.Contains(changedSignalCode)) continue;

    // 5. Evaluate gate (existing PG function)
    var gateActive = await EvaluateModuleGate(module, subjectId);
    if (!gateActive) continue;

    // 6. Extract relevant subgraph from Neo4j
    var subgraph = await ExtractSubgraph(subjectId, props.WatchSignals);

    // 7. Load prediction history from protocols
    var predictions = await protocolRepo.GetByModuleTag(module.Id, "PREDICTION");

    // 8. Build evolution context and call LLM
    var context = BuildEvolutionContext(module, subgraph, predictions);
    var llmOutput = await llm.CompleteAsync(evolutionPrompt, context);

    // 9. Parse LLM output → protocols → entities
    var lines = BioChainParser.Parse(llmOutput);
    foreach (var line in lines)
        await analyzeService.LinkComponentAsync(protocol, line, subjectId, ct);

    // 10. Update module lifecycle
    props.EvalCount++;
    props.LastEval = DateTime.UtcNow;
    await moduleRepo.UpdatePropertiesAsync(module.Id, props);
}
```

### Evolution Prompt (`SIGNALS_EVOLUTION_PROMPT.txt`)

The LLM receives:
- Current MODULE definition and its properties
- Relevant subgraph serialized as Signals Kernel DSL
- Prediction history (what was predicted vs what happened)
- Active constraints and their status

It outputs Signals Kernel DSL:
- `PREDICTION:` — new predictions for downstream signals
- `SIGNAL:` — updated signal assessments
- `CONSTRAINT:` — new or modified constraints
- `MODULE:` — new sub-MODULEs to spawn
- `BIND:` / `FAIL:` — updated protocol bindings
- Edge gain/delay adjustments via FORMULA tags

### Cypher Helper Methods

```csharp
// Extract 2-hop neighborhood around a set of signals
async Task<string> ExtractSubgraph(Guid subjectId, string[] signalCodes)
{
    var query = """
        MATCH (s {subject_id: $pid})
        WHERE s.code IN $codes
        OPTIONAL MATCH (s)-[r*1..2]-(neighbor)
        RETURN s, r, neighbor
    """;
    // Serialize to DSL format for LLM context
}

// Detect patterns that need new MODULEs
async Task<List<PatternMatch>> DetectPatterns(Guid subjectId)
{
    // Run a set of pattern-detection Cypher queries
    // Return matches for: feedback loops, cascades, orphan signals, etc.
}
```

---

## Dual-Mode Formula Execution

Formulas work **with and without numeric values**:

### Symbolic Mode (qualitative)
```
CORT[↑↑]@ADR → GLU[↑]@PFC via ⟳⁺ feedback
Agent reasons: "CORT is strongly elevated, positive feedback to GLU means excitotoxicity risk increases"
State symbols: ↓↓ < ↓ < ≈ < ↑ < ↑↑ (ordinal scale)
```

### Numeric Mode (quantitative)
```
CORT = 34 ug/dL, baseline = 10, boundary = [0, 25]
deviation_pct = 240%, trajectory = +2/day
Agent computes: "Already 36% over boundary, breached 4.5 days ago based on trajectory"
```

### Mixed Mode (most common)
Some signals have numeric values, others only have symbolic states. The agent works with whatever is available, using symbolic reasoning to fill gaps.

---

## Implementation Plan Summary

| Phase | What | Effort |
|-------|------|--------|
| 1 | Add `module.properties` lifecycle fields convention (no schema change) | Trivial |
| 2 | Add `PREDICTION` tag to parser + AnalyzeService handler | Small |
| 3 | Create `AgentEcosystemService` with LISTEN/debounce | Medium |
| 4 | Write Cypher queries for subgraph extraction + pattern detection | Medium |
| 5 | Create `SIGNALS_EVOLUTION_PROMPT.txt` | Medium |
| 6 | Wire up: gate eval → subgraph extract → LLM → parse → write back | Medium |
| 7 | Add MODULE spawning based on pattern detection | Medium |
| 8 | Add utility tracking + retirement logic | Small |

**Estimated total: ~500-700 lines of new C# code + 1 prompt file.**

Reuses: GraphSyncService pattern, evaluate_gate(), BioChainParser, AnalyzeService, LlmService, Neo4j driver, all existing repositories.

---

## Key Design Decisions

1. **No new tables** — module lifecycle in JSONB, predictions as protocols, patterns via Cypher
2. **Event-driven + LLM hybrid** — fast gate evaluation in PG, intelligent evolution via LLM
3. **Existing parser/service pipeline** — LLM output goes through same path as initial analysis
4. **Neo4j for pattern detection** — Cypher replaces vector-based RAG for structural patterns
5. **Dual-mode formulas** — agents reason symbolically or numerically based on data availability
6. **Self-closing loop** — agent writes to graph → triggers NOTIFY → potentially wakes other agents
