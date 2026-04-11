# BioChain

A 4-layer biochemical signal cascade pipeline that converts natural language clinical descriptions into structured BNF formulas, stores them as executable graph programs in SpacetimeDB, and enables simulation and conversational exploration.

## Architecture

```
                   natural language input
                           |
                     BioChain.Api (:13370)
                           |
                     BioChain.Service
                      /          \
              BioChain.Agent    BioChain.Models
                  |
          BioChain.SpacetimeClient
                  |
             SpacetimeDB (:3000)
              (biochain-module)
```

**Pipeline layers** run sequentially, each in its own context window:

| Layer | Purpose |
|-------|---------|
| **BASE** | Single-snapshot signal cascade analysis (chains, integration, protocol, conditional, EMIT) |
| **PLASTICITY** | Change map between BASE snapshots (delta cascades, binding kinetics) |
| **META** | Developmental/epigenetic program layer (architecture, remodeling, setpoints) |
| **CONVERGENCE** | Diamond closure computing convergence state from all prior layers |

## Projects

| Project | Stack | Role |
|---------|-------|------|
| `BioChain.Api` | .NET 10 / ASP.NET Core | REST API entry point |
| `BioChain.Service` | .NET 10 | LLM orchestration, pipeline execution, chat |
| `BioChain.Agent` | .NET 10 | SpacetimeDB agent, HTTP integration |
| `BioChain.Models` | .NET 10 | Shared data models |
| `BioChain.SpacetimeClient` | .NET 10 / SpacetimeDB SDK | Client for SpacetimeDB tables and reducers |
| `BioChain.AppHost` | .NET Aspire | Service orchestration host |
| `BioChain.ServiceDefaults` | .NET Aspire | OpenTelemetry, resilience, service discovery |
| `biochain-module` | Rust / SpacetimeDB 2.0 | WASM module: parser, validator, simulation engine, convergence |
| `biochain-tools` | Rust / axum | Reactome knowledge graph tool server (SQLite cache) |

## API Endpoints

```
POST /api/biochain/generate          Natural language -> LLM -> BNF -> parse -> validate
POST /api/biochain/ingest            Raw BNF text -> parse -> validate (skip LLM)
POST /api/biochain/simulate/{id}     Run perturbation simulation on a program
GET  /api/biochain/program/{id}      Get program state (nodes, edges, diagnostics)
POST /api/biochain/chat/{id}         Chat about a program's biochemical network
GET  /api/biochain/health            Health check
```

## Services

| Service | Image / Runtime | Port | Purpose |
|---------|----------------|------|---------|
| mcp | .NET 10 | 13370 | BioChain API server |
| vllm | vLLM | 8000 | Qwen3.5-A3B (LLM generation) |
| vllm-embed | vLLM | 8001 | Qwen3-Embedding-4B (vector embeddings) |
| postgres | PostgreSQL 16 + pgvector | 5434 | Relational store |
| neo4j | Neo4j + APOC/GDS | 7474/7687 | Graph database (LISTEN/NOTIFY sync from PG) |
| spacetimedb | SpacetimeDB | 3000 | Real-time relational DB for program state |
| biochain-tools | Rust/axum | 8002 | Reactome receptor/cascade/downstream lookups |

## Getting Started

### Prerequisites

- Docker & Docker Compose
- NVIDIA GPU with CUDA support (for vLLM)
- .NET 10 SDK (for local development)
- Rust toolchain (for module/tools development)

### Run

```bash
docker compose up -d
```

The API will be available at `http://localhost:13370`.

### Build from source

```bash
dotnet build BioChain.sln
```

### Publish the SpacetimeDB module

```bash
cd biochain-module
spacetimedb publish biochain
```

## Model Configuration

| Parameter | Value |
|-----------|-------|
| Model | Qwen3.5-A3B-AWQ-4B |
| Temperature | 0.3 |
| Top-p | 0.9 |
| Min-p | 0.05 |
| Presence penalty | 0.5 |
| Max tokens | dynamic (8192 - input - 200, cap 4000) |
| Thinking mode | disabled |

Optional constrained decoding via xgrammar EBNF files in `xgrammar/`.

## License

Proprietary.
