# Signals Kernel Runtime — Design Document v3 (Compact)

**Date:** 2026-03-03
**Branch:** `refactor/biochain-kernel`

---

## Constraints

- SQL schema UNCHANGED (14 tables, zero migrations)
- Protocol table stays (kernel writes to it)
- Postgres → Neo4j sync stays (one-way push)
- Kernel is additive C# on top of existing database

## 5 Source Files, Flat, No Folders

```
SignalsKernel/
  Compiler.cs          550 lines   AST + lexer + parser + lowering + vocabulary
  Engine.cs            500 lines   tick pipeline + 9 phases + FormulaVM + Extism
  Agent.cs             200 lines   side effects (Wolverine handlers + LLM bridge + tool bridge)
  Graph.cs             200 lines   Neo4j sync + GDS analysis wrappers
  Platform.cs          400 lines   Orleans grain + Marten events + gRPC + SignalR + fluent builder
  SignalsKernel.csproj
```

~1,850 lines total. Zero folders.

## Technology Stack

| Technology | Package | Version | Role |
|-----------|---------|---------|------|
| Apache Arrow | `Apache.Arrow` | 22.1.0 | Columnar signal state, SIMD vectorized phases |
| Extism | `Extism.Sdk` | 1.10.0 | WASM plugin host for custom gates/tools/transfer fns |
| Marten | `Marten` | >= 8.19.0 | Event sourcing on PG (kernel events, projections, snapshots) |
| Wolverine | `WolverineFx.Marten` | 5.13.0 | Side-effect bus (replaces custom ISideEffectScheduler) |
| Orleans | `Microsoft.Orleans.Sdk` | 9.2.x | Virtual actor grain per entity |
| gRPC | `Grpc.AspNetCore` | 2.x | Universal external API |
| Neo4j | `Neo4j.Driver` | 6.0.0 | Graph algorithms (GDS) |
| Npgsql | `Npgsql` | 9.0.3 | Raw SQL reads (3 DISTINCT ON per tick) |

## What Each File Owns

### Compiler.cs (550 lines)

Everything compile-time. Text in, execution plan out. No database calls.

**Contains:** AST node records (SignalDecl, EdgeDecl, GateDecl, FormulaDecl, ConstraintDecl, ToolDecl, LlmGateDecl, FailDecl, BindDecl, ModuleDecl, Expression tree), Token enum + Lexer, Parser (tokens → AST, Pratt expression parser), Lowering (AST → Postgres INSERTs + formula bytecode + topo sort + dead elimination), IVocabulary + data-driven Vocabulary class + built-in vocabs (bio, mkt, game, org, soc), `SignalsCompiler.Compile()` entry point.

**Why one file:** AST types exist to be produced by the parser and consumed by lowering. Vocabulary validates during lowering. They always change together.

### Engine.cs (500 lines)

Everything tick-time. Load state, compute, return results. The hot path.

**Contains:** Arrow-backed signal columns (value[], baseline[], confidence[], tau[] as DoubleArray), data types loaded from Postgres (SignalState, EdgeState, GateState), tick IO types (Input, TickResult, ProtocolEntry, SideEffect), TickCtx (mutable context through phases), TickPipeline (9 phases in sequence), all 9 phases (Resolve, Decay, Formula, Propagate, Gate, Constrain, Fail, Bind, Emit), gate evaluation logic (threshold, latch, and/or, integrator, llm-queue), constraint solver (boundary, equilibrium, conserve), fail checker (sustained, rate, oscillation, divergence), FormulaVM (stack bytecode interpreter, 16 opcodes), ExtismHost (WASM plugin host + registry + host functions).

**Arrow integration:** Signal state stored as Arrow RecordBatch columns. DecayPhase applies `tau * dt` as vectorized operation across entire column. PropagatePhase does vectorized multiply-accumulate. Zero-copy state streaming via Arrow IPC.

**Extism integration:** Custom transfer functions, gate evaluators, and tool handlers loaded as WASM modules. Sandboxed execution. Host functions expose signal state to plugins.

**Why one file:** When a tick produces wrong results, the entire execution path is here. No jumping between files.

### Agent.cs (200 lines)

Everything between ticks. Async IO dispatched after tick completes.

**Contains:** Wolverine message handlers for side-effect dispatch. LlmBridge (render prompt, call LLM, parse response, fallback). ToolBridge (route by invoke type: wasm/http/native, marshal refs).

**Wolverine integration:** Tick emits SideEffect records → published as Wolverine messages → handlers resolve asynchronously (LLM calls, tool invocations, external data) → results returned as inputs injected into next tick. Durable outbox guarantees delivery. Retry policies built in.

**Why one file:** All three classes are the same concern and reference each other.

### Graph.cs (200 lines)

Everything Neo4j. Existing sync pattern + GDS analysis.

**Contains:** Neo4jSync (RebuildGraph, SyncSignalValues, SyncGateStates — enriched with numeric properties), GDS wrappers (PageRank, Louvain, ShortestPath, MaxFlow, InfluencePath, CELF).

**Why one file:** All Cypher, all Neo4j driver, same connection.

### Platform.cs (400 lines)

Everything external-facing. How consumers use the kernel.

**Contains:** IWorldGrain + WorldGrain (Orleans thin shell), Marten event store integration (tick events → append → projections), gRPC service (CreateWorld, Inject, Tick, StreamEvents, GetState), SignalR hub, REST endpoints, World fluent builder and FromBnf/FromFile entry points.

**Marten integration:** Every tick's KernelEvents stored as Marten event stream per entity. Projections build current state. Snapshots for fast reload. Shares the existing PostgreSQL connection.

**Why one file:** The grain calls the engine, stores events via Marten, exposes via transports, and the builder creates grains. All "how do I use this" in one place.

## Data Flow

```
.signals text
      |
  Compiler.cs ──── Postgres INSERTs
                        |   (14 tables, unchanged)
                        |
          +-------------+---------------+
          |             |               |
      Engine.cs     Graph.cs        Agent.cs
      (tick loop)   (Neo4j sync)    (Wolverine handlers)
          |             |               |
          +-------------+---------------+
                        |
                   Platform.cs
                   (grain + Marten + gRPC)
```

**Read:** 3 DISTINCT ON queries per tick (signal, edge, gate).
**Write:** append-only INSERTs (signal, gate, protocol rows) + Marten events.
**Sync:** batch UNWIND to Neo4j (background).
**Side effects:** Wolverine dispatches LLM/tool calls between ticks, results injected next tick.

## Navigation

| Question | File |
|----------|------|
| How is BNF parsed? | Compiler.cs |
| What types can signals be? | Compiler.cs (vocabulary section) |
| How does a tick run? | Engine.cs |
| How do formulas execute? | Engine.cs (FormulaVM section) |
| How are gates evaluated? | Engine.cs (GatePhase section) |
| How do WASM plugins work? | Engine.cs (ExtismHost section) |
| What happens between ticks? | Agent.cs |
| How does Neo4j sync? | Graph.cs |
| How do consumers connect? | Platform.cs |
| How does the builder work? | Platform.cs (World section) |
| Where are events stored? | Platform.cs (Marten section) |

Every question maps to one file.

## Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Columnar data + SIMD -->
    <PackageReference Include="Apache.Arrow" Version="22.1.0" />
    <!-- WASM plugin host -->
    <PackageReference Include="Extism.Sdk" Version="1.10.0" />
    <!-- Event sourcing on PG -->
    <PackageReference Include="Marten" Version="8.19.0" />
    <!-- Side-effect bus + Marten integration -->
    <PackageReference Include="WolverineFx.Marten" Version="5.13.0" />
    <!-- Virtual actors -->
    <PackageReference Include="Microsoft.Orleans.Sdk" Version="9.2.0" />
    <PackageReference Include="Microsoft.Orleans.Persistence.Memory" Version="9.2.0" />
    <!-- gRPC -->
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <!-- Graph -->
    <PackageReference Include="Neo4j.Driver" Version="6.0.0" />
    <!-- Raw SQL reads -->
    <PackageReference Include="Npgsql" Version="9.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\src\BioChain.Models\BioChain.Models.csproj" />
    <ProjectReference Include="..\Kernel\BioChain.Kernel\BioChain.Kernel.csproj" />
  </ItemGroup>
</Project>
```

## Reference

- BNF spec: `src/BioChain.Repository/Data/BioSphere_Signal_BNF.txt` (1106 lines)
- EVAL ENGINE: BNF lines 680-729
- DB schema: `src/BioChain.Repository/Data/biochain_init.sql` (14 tables)
- Existing Kernel: `Kernel/BioChain.Kernel/` (Agents/, Graph/, Prompts/)
