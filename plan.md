# Plan: Create NeuroGateway.AnalysisFramework + Reorganize Repository + Activate Dead Components

## Goals
1. New `NeuroGateway.AnalysisFramework` class library — pure algorithms, config mapping, layer analysis
2. Services stay in Service as thin orchestrators calling framework functions
3. Move seed services + init.sql + DbContext into Repository (organized in folders)
4. Activate dead components + populate Layer.cs placeholders

## Architecture
```
AnalysisFramework = pure computation (refs only Models, no DB, no DI)
Service           = orchestration (DI, calls framework + repository)
Repository        = DB access + DbContext + seeding + init.sql
```

## Dependency Chain
```
Server → Service → AnalysisFramework → Models
                 → AgentFramework    → AnalysisFramework → Models
                 → Repository        → Models
```

---

## Phase 1: Reorganize Repository (move DbContext, seeds, init.sql)

### 1a. Create folder structure in Repository
```
NeuroGateway.Repository/
  Data/
    PersonalityDbContext.cs          ← moved from root
    init.sql                         ← moved from Server/init.sql
  Seed/
    KineticsSeedService.cs           ← moved from Service/
    AgentTemplateSeedService.cs      ← moved from Service/
  Entities/                          ← existing
  Configurations/                    ← existing
  *.cs (repositories)                ← existing
```

### 1b. Move PersonalityDbContext.cs → Repository/Data/
- Namespace stays `NeuroGateway.Repository` (no change needed)
- Just a file move into subfolder

### 1c. Move init.sql → Repository/Data/init.sql
- Update docker-compose.yml: `./NeuroGateway.Repository/Data/init.sql:/docker-entrypoint-initdb.d/init.sql`
- Update AppHost/Program.cs: `../NeuroGateway.Repository/Data/init.sql`
- Remove `NeuroGateway.Server/init.sql` (duplicate)
- Remove dead `resolve_algorithm_config` SQL function from init.sql

### 1d. Move KineticsSeedService.cs → Repository/Seed/
- Namespace: `NeuroGateway.Repository.Seed`
- Already depends on `IDbContextFactory<PersonalityDbContext>` + `AlgorithmConfigLoader`
- After AnalysisFramework exists: depends on `KineticsDataBuilder` (framework) for pure data + DB writes itself
- Repository.csproj needs ref to AnalysisFramework (for AlgorithmConfigLoader + KineticsDataBuilder)

### 1e. Move AgentTemplateSeedService.cs → Repository/Seed/
- Namespace: `NeuroGateway.Repository.Seed`
- Already depends on `IDbContextFactory<PersonalityDbContext>` + `AlgorithmConfigLoader` + `AgentTemplateRepository`
- Natural fit — it seeds DB tables

### 1f. Update Program.cs
- DI registrations: `NeuroGateway.Repository.Seed.KineticsSeedService`, `NeuroGateway.Repository.Seed.AgentTemplateSeedService`
- Remove seed service registrations from Service project references

### 1g. Update Service.csproj
- Remove YamlDotNet (only needed by seed services, which move to Repository)
- Unless other Service files still use it — check first

---

## Phase 2: Create NeuroGateway.AnalysisFramework project

### 2a. Create project + add to solution
- `dotnet new classlib -n NeuroGateway.AnalysisFramework`
- Add to solution under `Frameworks` folder
- References: `NeuroGateway.Models` ONLY (pure computation)
- Package: YamlDotNet (for AlgorithmConfigLoader)

### 2b. Move pure algorithms (from AgentFramework/Algorithms/)
- `VectorAlgorithms.cs` → `AnalysisFramework/Algorithms/`
- `ClusteringAlgorithms.cs` → `AnalysisFramework/Algorithms/`
- Namespace: `NeuroGateway.AnalysisFramework.Algorithms`
- Delete originals from AgentFramework/Algorithms/

### 2c. Move AlgorithmConfigLoader (from AgentFramework/)
- `AlgorithmConfigLoader.cs` → `AnalysisFramework/`
- Namespace: `NeuroGateway.AnalysisFramework`
- Delete original

### 2d. Extract ConfigResolver.cs (pure math from DynamicConfigResolver)
**New: `AnalysisFramework/ConfigResolver.cs`** — static class, pure math:
- `MapToConfig(Dictionary<string, double>)` → `ResolvedAlgorithmConfig`
- `BlendWithKinetics(ResolvedAlgorithmConfig, List<KineticsHit>, float)` → adjusted config
- Ordinal encode/decode (InterpretationOrdinals, StrategyOrdinals, etc.)
- `CategorySignal` record
- `Clamp()` helper

**Thin: `Service/DynamicConfigResolver.cs`** keeps:
- ConcurrentDictionary cache
- DB calls via configRepo/kineticsRepo
- Calls `ConfigResolver.MapToConfig()` and `ConfigResolver.BlendWithKinetics()`

### 2e. Extract HeatmapComputation.cs (pure math from VectorService)
**New: `AnalysisFramework/HeatmapComputation.cs`** — static class:
- `Compute(targets, entries, topN)` → `List<HormoneTraitHeatmap>`
- Pure cosine similarity + ranking, no DB

**Thin: `Service/VectorService.cs`** keeps:
- DB calls (`_embeddingRepo.GetTargetEmbeddingsAsync()`)
- Config resolution (`_configResolver.ResolveAsync()`)
- Calls `HeatmapComputation.Compute()`

### 2f. Extract KineticsDataBuilder.cs (pure data from KineticsSeedService)
**New: `AnalysisFramework/KineticsDataBuilder.cs`** — static class:
- `GetKineticsData()` → hardcoded research data (~36 rows)
- `FlattenYamlDefaults/Modes/Layers(...)` → parameter name/value pairs
- `ToDouble(object?)` → value conversion with ordinal encoding
- Ordinal dictionaries

**Thin: `Repository/Seed/KineticsSeedService.cs`** keeps:
- DB writes (`INSERT ... ON CONFLICT`)
- Calls `KineticsDataBuilder.*()` for data generation

### 2g. New: LayerAnalysis.cs (the big addition)
**New: `AnalysisFramework/LayerAnalysis.cs`** — static class, pure math:

Computes placeholder values for Layer.cs from centroids + config:

```csharp
public static class LayerAnalysis
{
    public static LayerAnalysisResult Compute(
        float[]? ntCentroid, float[]? hormoneCentroid, float[]? peptideCentroid,
        CoherenceConfig coherenceConfig, SubspaceConfig subspaceConfig)
    {
        var coherenceScore = ComputeCoherence(...);
        var interpretation = coherenceScore < coherenceConfig.LowThreshold
            ? coherenceConfig.Interpretation : "aligned";
        var conflictAxis = FindConflictAxis(...);
        var subspaceGaps = ComputeSubspaceGaps(...);
        return new LayerAnalysisResult(...);
    }

    // Coherence: weighted pairwise cosine sim between centroids
    // Conflict: lowest-similarity pair
    // Subspace: 16×256 band divergence analysis
}
```

### 2h. New model: LayerAnalysisResult.cs
```csharp
public record LayerAnalysisResult(
    float CoherenceScore,
    string CoherenceInterpretation,
    string? ConflictAxis,
    string? SubspaceGaps,
    string? DriftSummary,       // null until temporal tracking
    string? DriftVelocity,      // null until temporal tracking
    string? AttractorPattern,   // null until temporal tracking
    string? NtSpread, string? HormoneSpread, string? PeptideSpread);
```

### 2i. New model: KineticsHit.cs (replaces KineticsRow for framework boundary)
```csharp
public record KineticsHit(
    string ParameterName, double ParameterValue,
    string Category, double Similarity);
```
Used by `ConfigResolver.BlendWithKinetics()`. Maps 1:1 from `KineticsRepository.KineticsRow`.

---

## Phase 3: Wire into neurorespond pipeline

### 3a. ProfileScoringService returns centroids
Currently centroids are computed internally (MeanPool) but not exposed.
Add `LayerCentroids` to return type:
```csharp
public record LayerCentroids(float[]? Nt, float[]? Hormone, float[]? Peptide);
// Return: (LayerEstimation, BlendedProfiles, LayerCentroids)
```

### 3b. Activate kinetics blending
`ProfileScoringService.GetDualScoredProfile` line 88:
```csharp
// Before:
var config = await configResolver.ResolveAsync(mode, layer);
// After:
var config = await configResolver.ResolveAsync(mode, layer,
    conversationEmbedding: messageEmbedding, blendStrength: 0.3f);
```

### 3c. NeuroService calls LayerAnalysis
Between scoring (step 4) and layer agents (step 7):
```csharp
var ntConfig = await _configResolver.ResolveAsync(resolvedRelationship, "neurotransmitter");
var analysis = LayerAnalysis.Compute(
    centroids.Nt, centroids.Hormone, centroids.Peptide,
    ntConfig.Coherence, ntConfig.Subspace);
```

### 3d. Layer.cs accepts + uses LayerAnalysisResult
Add `LayerAnalysisResult? analysis = null` parameter to `RunLayerResponseAsync`.
Replace "N/A" placeholders:
```csharp
// Layer agents:
.Replace("{layer_spread}", analysis?.NtSpread ?? "N/A")  // per-layer
.Replace("{drift_velocity}", analysis?.DriftVelocity ?? "N/A")
.Replace("{subspace_divergence}", analysis?.SubspaceGaps ?? "N/A")
// Synthesizer:
.Replace("{coherence_score}", analysis?.CoherenceScore.ToString("F2") ?? "N/A")
.Replace("{coherence_interpretation}", analysis?.CoherenceInterpretation ?? "N/A")
.Replace("{conflict_axis}", analysis?.ConflictAxis ?? "N/A")
.Replace("{drift_summary}", analysis?.DriftSummary ?? "N/A")
.Replace("{attractor_pattern}", analysis?.AttractorPattern ?? "N/A")
.Replace("{subspace_gaps}", analysis?.SubspaceGaps ?? "N/A")
```

---

## Phase 4: Update project references + DI

### Project references
- `AnalysisFramework.csproj` → refs: Models (+ YamlDotNet package)
- `AgentFramework.csproj` → replace internal Algorithms/ with ref to AnalysisFramework
- `Repository.csproj` → add ref to AnalysisFramework (for seed services using AlgorithmConfigLoader + KineticsDataBuilder)
- `Service.csproj` → add ref to AnalysisFramework

### Program.cs DI
```csharp
// AnalysisFramework
services.AddSingleton<NeuroGateway.AnalysisFramework.AlgorithmConfigLoader>();
// Repository/Seed (moved from Service)
services.AddSingleton<NeuroGateway.Repository.Seed.KineticsSeedService>();
services.AddSingleton<NeuroGateway.Repository.Seed.AgentTemplateSeedService>();
```

### Using updates
- `NeuroGateway.AgentFramework.Algorithms` → `NeuroGateway.AnalysisFramework.Algorithms`
- `NeuroGateway.Service.KineticsSeedService` → `NeuroGateway.Repository.Seed.KineticsSeedService`
- `NeuroGateway.Service.AgentTemplateSeedService` → `NeuroGateway.Repository.Seed.AgentTemplateSeedService`

---

## File Summary

### Moved
| File | From | To |
|------|------|----|
| `VectorAlgorithms.cs` | `AgentFramework/Algorithms/` | `AnalysisFramework/Algorithms/` |
| `ClusteringAlgorithms.cs` | `AgentFramework/Algorithms/` | `AnalysisFramework/Algorithms/` |
| `AlgorithmConfigLoader.cs` | `AgentFramework/` | `AnalysisFramework/` |
| `PersonalityDbContext.cs` | `Repository/` | `Repository/Data/` |
| `init.sql` | `Server/` (+ root) | `Repository/Data/` |
| `KineticsSeedService.cs` | `Service/` | `Repository/Seed/` |
| `AgentTemplateSeedService.cs` | `Service/` | `Repository/Seed/` |

### New (AnalysisFramework)
| File | Purpose |
|------|---------|
| `ConfigResolver.cs` | Config mapping + kinetics blending (pure math) |
| `LayerAnalysis.cs` | Coherence, conflict axis, subspace computation |
| `HeatmapComputation.cs` | Heatmap scoring (pure math) |
| `KineticsDataBuilder.cs` | Research data + YAML flattening |

### New (Models)
| File | Purpose |
|------|---------|
| `LayerAnalysisResult.cs` | Layer analysis result record |
| `KineticsHit.cs` | Framework-boundary DTO for kinetics rows |

### Modified (thinned out)
| File | Change |
|------|--------|
| `Service/DynamicConfigResolver.cs` | Delegates math to ConfigResolver |
| `Service/VectorService.cs` | Delegates math to HeatmapComputation |
| `Service/ProfileScoringService.cs` | Returns centroids; blendStrength=0.3 |

### Modified (wiring)
| File | Change |
|------|--------|
| `sln` | Add AnalysisFramework |
| `AgentFramework.csproj` | Ref AnalysisFramework |
| `Repository.csproj` | Ref AnalysisFramework |
| `Service.csproj` | Ref AnalysisFramework; remove YamlDotNet if unused |
| `Layer.cs` | Accept + use LayerAnalysisResult |
| `NeuroService.cs` | Inject DynamicConfigResolver, call LayerAnalysis.Compute, pass to Layer |
| `Program.cs` | Update DI registrations |
| `docker-compose.yml` | Update init.sql path |
| `AppHost/Program.cs` | Update init.sql path |
| `Repository/Data/init.sql` | Remove resolve_algorithm_config function |

## Dead → Live Status

| Component | Before | After |
|-----------|--------|-------|
| BlendWithKineticsAsync | DEAD | LIVE (blendStrength=0.3) |
| KineticsRepository.GetRelevantKineticsAsync | DEAD | LIVE |
| resolve_algorithm_config SQL | DEAD | REMOVED |
| GetChemicalOverridesAsync | DEAD | DEFERRED (needs per-chemical scoring) |
| Layer.cs 9 placeholders | "N/A" | LIVE (coherence, interpretation, conflict, subspace) |
