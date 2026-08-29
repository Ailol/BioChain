# Chain Core

```
Co-author: Ailo > its life, day to day, and to minmax
Co-author: Claude >this is a structured signal cascade analysis.
```
The notion'
> Yesterday you put in what you ate, today you wake up with "good/bad simulations about the day"
> The cascade can on a bio'engineers build a quick "eat this, drink this, 3 minuts and you good again <3"

Universal diamond compiler for structured signal cascade analysis. Converts domain-specific BNF formulas into executable graph programs via a 4-layer pipeline (BASE → PLASTICITY → META → CONVERGENCE), stores them in SpacetimeDB, and enables simulation and convergence analysis.

The compiler is domain-agnostic. Domain-specific vocabulary (node types, regions, cascade tags, semantic rules) is supplied via domain packs. The grammar, operators, state arrows, closure invariants, and cross-layer linking rules are universal across all Chain domains.

## Architecture

```
                 BNF text (any domain)
                        |
                   parser_core.rs          tokenizer + BNF parser
                        |
                    parser.rs              SpacetimeDB ingest
                        |
                   validator/
                     structural.rs         pass 1: universal closure invariants
                     vocabulary.rs         pass 2: tokens vs domain pack (stub)
                     semantic.rs           pass 3: domain rule dispatch (stub)
                        |
                   executor.rs             tick-based simulation
                        |
               convergence_engine.rs       three-vector diamond closure
```

## Domain Packs

Two domain specs exist as reference in `system-prompts/`:

| Domain | Subject | Node Types | Regions | Cascade Tags |
|--------|---------|------------|---------|--------------|
| **BioChain** | Neuroscience / biochemistry | L.nt R Gp 2m K TF G N.* B.* | PFC AMY HPC DRN VTA ENS ... | GPCR.Gs NUCLEAR RTK VAGAL ... |
| **LogicChain** | Reasoning / epistemics | P C I H D Q F V.val E.evi Mo Mem Att | WM LTM SELF MORAL EMPIRICAL ... | DEDUCTIVE BAYESIAN HEURISTIC ... |

The grammar is identical across both. Only vocabulary slots differ.

## Project Structure

```
chain-core/                        # Rust/SpacetimeDB WASM module
  src/
    lib.rs
    parser_core.rs                 # tokenizer + BNF parser
    parser.rs                      # SpacetimeDB ingest reducers
    validator/                     # 3-pass validation
      structural.rs                # universal closure invariants
      vocabulary.rs                # domain pack vocabulary check (stub)
      semantic.rs                  # domain semantic rules (stub)
    ir/                            # universal IR (stub)
    domain_pack/                   # DomainPack trait (stub)
    executor.rs                    # tick simulation engine
    convergence_engine.rs          # three-vector convergence
    differ.rs                      # snapshot diff
    reconstruct.rs                 # DB → BNF inverse compiler
    types.rs                       # core data structures
    db/                            # SpacetimeDB persistence layer
      base/                        # BASE layer tables + reducers
      plasticity/                  # PLASTICITY layer
      meta/                        # META layer
      convergence/                 # CONVERGENCE layer
      sim/                         # simulation tables
  generated-bindings/              # C# SpacetimeDB client SDK

biochain-tools/                    # Reactome knowledge graph server (Rust/axum)
system-prompts/
  BioChain/                        # domain spec: neuroscience
  LogicChain/                      # domain spec: reasoning/epistemics
xgrammar/                          # EBNF grammar specs
neo4j/                             # graph init scripts
docker-compose.yml                 # postgres + neo4j + spacetimedb + biochain-tools
```

## Universal Operators

| Layer | Operators |
|-------|-----------|
| BASE | `∫` integration, `⊲` protocol, `⊗` conditional, `◈` composite, `⚡` dysreg, `⊕` observable |
| PLASTICITY | `Δ0-Δ3` delta ranks, `⊟` cascade |
| META | `σ̃` setpoint, `⊲̃` rule program, `∫̃` structural program, `⊗̃` architecture |
| CONVERGENCE | `∮` contour, `⊳` trajectory, `⊳⚠` risk, `⚡` flags, `⊕⊳` monitor |

Edges: `→` activate, `⊣` inhibit, `~>` modulate, `=>` transcribe/instantiate, `|>` transport/retrieve

State arrows: `-- - = ~ + ++ X *`

## Getting Started

### Prerequisites

- Rust toolchain
- Docker & Docker Compose
- SpacetimeDB CLI

### Build

```bash
cd chain-core
cargo build
```

### Infrastructure

```bash
docker compose up -d
```

### Publish module

```bash
spacetime publish --server http://localhost:3000 biochain ./chain-core
```

## Services

| Service | Port | Purpose |
|---------|------|---------|
| spacetimedb | 3000 | Program state (nodes, edges, deltas, convergence) |
| postgres | 5434 | Relational storage (pgvector) |
| neo4j | 7474/7687 | Graph database |
| biochain-tools | 8002 | Reactome receptor/cascade lookups |
