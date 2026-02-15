# NeuroGateway Kinetics System — Complete Specification for Config Revision

## Purpose of This Document

This document describes the **neurochemical kinetics RAG system** in NeuroGateway — a conversation-adaptive algorithm parameter blending pipeline. The system is fully built but the kinetics blending path is currently disconnected (no caller passes conversation embeddings to the config resolver). The goal is to revise the config so that:

1. The kinetics rows are properly categorized to produce meaningful parameter adjustments
2. The blend rules in `ConfigResolver.BlendWithKinetics` map research signals to the right algorithm knobs
3. The system can be wired with a single code change at each call site

---

## 1. Architecture — How the Pieces Fit

### System Overview

```
AlgorithmConstants.yaml ──(startup seed)──> 6 DB tables
                                              │
                                              ▼
                                   DynamicConfigResolver
                                     ┌────────┴────────┐
                                     │                  │
                              Static Path          Kinetics Path
                              (ACTIVE)             (BUILT, NOT WIRED)
                                     │                  │
                                     ▼                  ▼
                           3-step cascade:        cosine similarity
                           defaults→layer→mode    against neurochemical_kinetics
                                     │                  │
                                     ▼                  ▼
                           ConfigResolver.MapToConfig   ConfigResolver.BlendWithKinetics
                                     │                  │
                                     └────────┬────────┘
                                              ▼
                                   ResolvedAlgorithmConfig
                                   (7 typed sub-records)
                                              │
                                              ▼
                                   Used by: ProfileScoringService,
                                   NeuroService, PersonalityService,
                                   VectorService, LayerAnalysis
```

### The Two Paths

**Static path (currently active):** `DynamicConfigResolver.ResolveAsync(mode, layer)` → reads from `algorithm_parameter_definitions` + `layer_parameter_overrides` + `mode_parameter_overrides` → `ConfigResolver.MapToConfig()` → cached `ResolvedAlgorithmConfig`.

**Kinetics path (built, not wired):** `DynamicConfigResolver.ResolveAsync(mode, layer, conversationEmbedding, blendStrength)` → static config + cosine similarity search against `neurochemical_kinetics.description_embedding` → `ConfigResolver.BlendWithKinetics()` → adjusted `ResolvedAlgorithmConfig`.

### What Needs to Change to Wire It

Currently every caller uses the 2-param form:
```csharp
// NeuroService.cs:67
var config = await _configResolver.ResolveAsync(resolvedRelationship, "neurotransmitter");

// ProfileScoringService.cs:90
var config = await configResolver.ResolveAsync(mode, layer);

// PersonalityService.cs:346
var clusterConfig = await _configResolver.ResolveAsync("dating", layerName);

// VectorService.cs:23
var config = await configResolver.ResolveAsync("dating", "neurotransmitter");
```

To activate kinetics blending, the callers that have a conversation embedding available need to pass it:
```csharp
// NeuroService.cs — inputEmbedding is already available (line 55)
var config = await _configResolver.ResolveAsync(resolvedRelationship, "neurotransmitter",
    inputEmbedding, blendStrength);

// ProfileScoringService.cs — inputEmbedding is a parameter
var config = await configResolver.ResolveAsync(mode, layer,
    inputEmbedding, blendStrength);
```

---

## 2. The Static Config — What Gets Resolved Before Kinetics

### 2.1 Parameter Definitions (defaults)

These are the base values from `algorithm_parameter_definitions` table (seeded from AlgorithmConstants.yaml `defaults:` section):

| Parameter Name | Default | Group | Description |
|---|---|---|---|
| `scoring.message_weight` | 0.55 | scoring | How much the current message embedding matters vs profile history (0=all history, 1=all message) |
| `scoring.freshness_boost` | 1.3 | scoring | Multiplier for recent observations in scoring |
| `scoring.min_similarity` | 0.15 | scoring | Minimum cosine similarity to include a profile row in results |
| `scoring.top_per_chemical` | 1 | scoring | How many top-scoring rows to keep per chemical |
| `scoring.freshness_half_life_days` | 30 | scoring | Days until a profile observation's freshness decays to 50% |
| `scoring.freshness_floor` | 0.7 | scoring | Minimum freshness score (never drops below this) |
| `scoring.freshness_amplitude` | 0.3 | scoring | Range of freshness variation (floor + amplitude = 1.0 at time=0) |
| `pooling.strategy` | attentive (0) | pooling | How multiple embeddings are pooled into a centroid |
| `pooling.attention_temperature` | 0.8 | pooling | Softmax temperature for attentive pooling (lower = sharper focus) |
| `pooling.temporal_half_life` | 15 | pooling | Half-life in days for temporal decay weighting |
| `pooling.min_entries_for_strategy` | 5 | pooling | Minimum entries before advanced pooling kicks in |
| `drift.shift_threshold` | 0.12 | drift | Cosine distance change that constitutes a "real" drift |
| `drift.std_dev_multiplier` | 1.5 | drift | How many std devs above baseline = significant drift |
| `drift.velocity_window` | 5 | drift | Number of recent observations for drift velocity calculation |
| `drift.noise_floor` | 0.04 | drift | Minimum drift magnitude to consider (below = noise) |
| `drift.subspace_drift_weight` | 0.5 | drift | Weight of subspace-level drift vs global drift |
| `coherence.low_threshold` | 0.45 | coherence | Below this cosine similarity = low cross-layer coherence |
| `coherence.interpretation` | neutral (3) | coherence | What low coherence means: opportunity/alarm/expected/neutral/boundary |
| `coherence.layer_pair_weights.nt_hormone` | 0.33 | coherence | Weight of NT-hormone pair in coherence calculation |
| `coherence.layer_pair_weights.nt_peptide` | 0.33 | coherence | Weight of NT-peptide pair in coherence calculation |
| `coherence.layer_pair_weights.hormone_peptide` | 0.34 | coherence | Weight of hormone-peptide pair in coherence calculation |
| `attractors.threshold` | 0.82 | attractors | Cosine similarity to classify a state as revisiting an attractor |
| `attractors.min_visits` | 2 | attractors | Minimum visits to declare an attractor |
| `attractors.max_attractors` | 8 | attractors | Maximum tracked attractors per layer |
| `attractors.track_transitions` | false (0) | attractors | Whether to track state transitions between attractors |
| `clustering.threshold` | 0.78 | clustering | Cosine distance threshold for write-time clustering |
| `clustering.k_neighbors` | 3 | clustering | K for k-NN at read time |
| `clustering.min_cluster_size` | 2 | clustering | Minimum members for a valid cluster |
| `subspace.num_bands` | 16 | subspace | Number of subspace bands (16 x 256 = 4096) |
| `subspace.band_dim` | 256 | subspace | Dimension per band |
| `subspace.divergence_threshold` | 0.40 | subspace | Cross-layer divergence threshold |

### 2.2 Layer Overrides

Applied after defaults, before mode overrides. Key differences from defaults:

**Neurotransmitter layer** (fast signals, ms-s):
- `pooling.strategy` → `attentive_temporal_decay` (aggressive recent-weighting)
- `pooling.temporal_half_life` → 5 (fast decay)
- `drift.velocity_window` → 3 (fast measurement)
- `scoring.top_per_chemical` → 2 (more rows for noisy layer)
- `clustering.threshold` → 0.70 (more clusters, diverse signals)

**Hormone layer** (slow signals, hours-days):
- `pooling.strategy` → `confidence_weighted`
- `pooling.temporal_half_life` → 20
- `drift.velocity_window` → 8
- `clustering.threshold` → 0.80 (fewer broader clusters)

**Peptide layer** (structural signals, weeks-lifetime):
- `pooling.strategy` → `confidence_weighted`
- `pooling.temporal_half_life` → 50
- `drift.velocity_window` → 15
- `clustering.threshold` → 0.85 (tight structural clusters)

### 2.3 Mode Overrides

Applied last (most specific wins). 10 modes: dating, partner, relationship, family, conflict, friend, mindhat, colleague, exwife, acquaintance.

Key mode differences:

| Mode | message_weight | shift_threshold | coherence.interpretation | attention_temperature |
|---|---|---|---|---|
| dating | 0.65 | 0.05 | opportunity | 0.3 |
| partner | 0.50 | 0.10 | alarm | 0.8 |
| family | 0.35 | 0.25 | alarm | 1.5 |
| conflict | 0.80 | 0.08 | expected | 0.2 |
| exwife | 0.75 | 0.10 | boundary | 0.4 |
| acquaintance | 0.50 | 0.20 | neutral | 1.5 |

### 2.4 Resolution Example

For mode=`dating`, layer=`neurotransmitter`:

```
scoring.message_weight:     0.55 (default) → 0.65 (dating mode override)
scoring.top_per_chemical:   1 (default) → 2 (NT layer override) → not overridden by dating
pooling.strategy:           attentive (default) → attentive_temporal_decay (NT layer override)
pooling.attention_temperature: 0.8 (default) → 0.3 (dating mode override)
drift.shift_threshold:      0.12 (default) → 0.05 (dating mode override)
drift.velocity_window:      5 (default) → 3 (NT layer override)
coherence.interpretation:   neutral (default) → opportunity (dating mode override)
clustering.threshold:       0.78 (default) → 0.70 (NT layer override) → not overridden by dating
```

---

## 3. The Kinetics RAG System — What Exists

### 3.1 Database Schema

```sql
CREATE TABLE neurochemical_kinetics (
    id SERIAL PRIMARY KEY,
    layer TEXT NOT NULL,                    -- neurotransmitter | hormone | peptide
    chemical TEXT NOT NULL,                 -- dopamine, cortisol, oxytocin, etc.
    category TEXT NOT NULL,                 -- temporal | drift | scoring | interaction | mode_signal
    parameter_name TEXT NOT NULL,           -- what this row measures
    parameter_value DOUBLE PRECISION NOT NULL, -- the research value
    unit TEXT,                              -- ms, hz, min, ratio, percent, etc.
    confidence TEXT NOT NULL DEFAULT 'estimated', -- measured | estimated
    description TEXT NOT NULL,              -- what this fact means (EMBEDDED for similarity search)
    context TEXT,                           -- when this fact is relevant
    source TEXT,                            -- citation
    related_modes TEXT[],                   -- which relationship modes this applies to
    description_embedding vector(4096),     -- embedding of description+context for RAG retrieval
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ
);
```

### 3.2 Current Kinetics Data (~36 rows)

Every row has a `description` that gets embedded for similarity search, and a `context` that describes when the fact is relevant.

#### Category: `temporal` (10 rows)
Research-backed timing data. Currently used only as knowledge base for retrieval — NOT directly mapped to algorithm parameters.

| Chemical | Parameter | Value | Unit | Description (abbreviated) |
|---|---|---|---|---|
| dopamine | synaptic_clearance_ms | 200 | ms | DAT reuptake peak-to-baseline ~1s |
| dopamine | tonic_firing_hz | 4 | hz | VTA baseline firing 2-5 Hz |
| dopamine | phasic_burst_hz | 25 | hz | VTA phasic burst 15-40 Hz |
| dopamine | receptor_integration_timescale_min | 5 | min | D1/D2 integrate over minutes despite fast clearance |
| serotonin | synaptic_clearance_s | 2 | s | Slower than DA, mood shifts are gradual |
| serotonin | autoreceptor_desensitization_weeks | 2.5 | weeks | 5-HT1A desensitization (SSRI lag) |
| norepinephrine | lc_tonic_firing_hz | 2 | hz | LC baseline for optimal attention |
| norepinephrine | phasic_burst_hz | 15 | hz | LC phasic to salient stimuli |
| gaba | fast_ipsc_decay_ms | 4 | ms | GABA-A fast IPSC |
| gaba | tonic_current_pa | 35 | pA | Extrasynaptic delta-subunit tonic current |

#### Category: `temporal` (hormone/peptide, 10 rows)
| Chemical | Parameter | Value | Unit | Description (abbreviated) |
|---|---|---|---|---|
| cortisol | plasma_half_life_min | 78 | min | 66-90 min plasma half-life |
| cortisol | car_peak_min | 30 | min | Cortisol Awakening Response peak |
| oxytocin | plasma_half_life_min | 4 | min | Peripheral OT fleeting |
| oxytocin | csf_half_life_min | 20 | min | Central OT much longer |
| testosterone | social_response_latency_min | 17 | min | T response to status challenge |
| testosterone | diurnal_variation_pct | 56 | percent | 50-63% morning-to-evening decline |
| endorphins | csf_half_life_min | 93 | min | Beta-endorphin central persistence |
| npy | anxiolytic_duration_min | 45 | min | Y1 receptor anxiolytic duration |
| npy | resilience_window_weeks | 8 | weeks | Repeated NPY builds lasting resilience |
| crh | behavioral_anxiety_duration_hours | 2.5 | hours | CRH anxiety persists long after plasma clearance |
| bdnf | mrna_peak_hours | 2 | hours | BDNF mRNA peaks 1-3h after activity |

#### Category: `interaction` (1 row)
| Chemical | Parameter | Value | Unit | Description |
|---|---|---|---|---|
| oxytocin | cortisol_attenuation_effect | -0.151 | hedges_g | Meta-analytic OT→cortisol effect: modest, NOT significant in healthy populations |

#### Category: `scoring` (5 rows)
Direct algorithm parameter suggestions from research.

| Chemical | Parameter | Value | Unit | Description (abbreviated) |
|---|---|---|---|---|
| cortisol | message_weight | 0.45 | ratio | Hormones are slow → history matters more |
| dopamine | message_weight | 0.65 | ratio | NTs are fast → current message matters more |
| oxytocin (peptide) | message_weight | 0.30 | ratio | Attachment is structural → profile dominates |
| dopamine | freshness_half_life_days | 3 | days | NT observations decay fast |
| cortisol | freshness_half_life_days | 14 | days | Hormone observations persist weeks |
| oxytocin (peptide) | freshness_half_life_days | 60 | days | Attachment observations persist months |

#### Category: `drift` (4 rows)
| Chemical | Parameter | Value | Unit | Description (abbreviated) |
|---|---|---|---|---|
| cortisol | responder_threshold_pct | 15.5 | percent | Optimal stress responder threshold |
| cortisol | recovery_to_baseline_min | 75 | min | Post-stressor recovery time |
| cortisol | shift_threshold | 0.12 | cosine_delta | Medium drift threshold for hormones |
| dopamine | shift_threshold | 0.05 | cosine_delta | Low threshold for fast NTs |
| oxytocin (peptide) | shift_threshold | 0.25 | cosine_delta | High threshold for structural peptides |

#### Category: `mode_signal` (3 rows)
Coherence interpretation overrides per mode.

| Chemical | Parameter | Value | Unit | Description (abbreviated) |
|---|---|---|---|---|
| oxytocin | coherence_dating_interpretation | 1 | code | Low coherence in dating = OPPORTUNITY |
| cortisol | coherence_family_interpretation | 3 | code | Low coherence in family = ALARM |
| cortisol | coherence_conflict_interpretation | 2 | code | Low coherence in conflict = EXPECTED |

### 3.3 The Retrieval Step

```csharp
// KineticsRepository.GetRelevantKineticsAsync
SELECT
    nk.parameter_name,
    nk.parameter_value,
    nk.category,
    nk.chemical,
    nk.description,
    1.0 - (nk.description_embedding <=> @embedding::vector) AS similarity
FROM neurochemical_kinetics nk
WHERE nk.layer = @layer
  AND nk.description_embedding IS NOT NULL
ORDER BY nk.description_embedding <=> @embedding
LIMIT 20
```

**Input:** The conversation embedding (the user's message embedded as float[4096]).
**Output:** Up to 20 kinetics rows ordered by cosine similarity to the conversation, with their similarity scores.

**What this means:** If someone says "I can't stop thinking about her", the embedding will be close to dopamine temporal rows about "phasic bursts triggered by unexpected rewards" and "variable reinforcement schedule". It will also be close to serotonin rows about "mood shifts are gradual" (because obsessive thinking maps to 5-HT). The similarity scores tell us which research facts are most relevant to what was actually said.

### 3.4 The Blend Step (ConfigResolver.BlendWithKinetics)

```csharp
public static ResolvedAlgorithmConfig BlendWithKinetics(
    ResolvedAlgorithmConfig staticConfig,
    List<KineticsHit> kinetics,
    float blendStrength)
{
    // Group hits by category
    var byCategory = kinetics
        .GroupBy(k => k.Category)
        .ToDictionary(
            g => g.Key,
            g => new CategorySignal(
                AvgSimilarity: g.Average(k => k.Similarity),
                MaxSimilarity: g.Max(k => k.Similarity),
                TotalWeight: g.Sum(k => k.Similarity),
                Count: g.Count(),
                WeightedAvgValue: g.Sum(k => k.ParameterValue * k.Similarity)
                                / g.Sum(k => k.Similarity)));

    // Category: temporal → increase message_weight
    if (byCategory.TryGetValue("temporal", out var temporal))
    {
        var temporalInfluence = (float)(temporal.AvgSimilarity * blendStrength);
        scoring = scoring with {
            MessageWeight = Clamp(scoring.MessageWeight + temporalInfluence * 0.1f, 0.1f, 0.95f)
        };
    }

    // Category: drift → decrease shift_threshold (more sensitive)
    if (byCategory.TryGetValue("drift", out var driftSignal))
    {
        var driftInfluence = (float)(driftSignal.AvgSimilarity * blendStrength);
        drift = drift with {
            ShiftThreshold = Clamp(drift.ShiftThreshold - driftInfluence * 0.02f, 0.02f, 0.5f)
        };
    }

    // Category: scoring → blend toward research-suggested value
    if (byCategory.TryGetValue("scoring", out var scoringSignal))
    {
        var scoringInfluence = (float)(scoringSignal.AvgSimilarity * blendStrength);
        var researchWeight = (float)scoringSignal.WeightedAvgValue;
        scoring = scoring with {
            MessageWeight = Clamp(
                scoring.MessageWeight * (1 - scoringInfluence)
                    + researchWeight * scoringInfluence,
                0.1f, 0.95f)
        };
    }

    // Category: interaction → decrease coherence low_threshold
    if (byCategory.TryGetValue("interaction", out var interaction))
    {
        var interactionInfluence = (float)(interaction.AvgSimilarity * blendStrength);
        coherence = coherence with {
            LowThreshold = Clamp(
                coherence.LowThreshold - interactionInfluence * 0.05f, 0.2f, 0.7f)
        };
    }

    // mode_signal category: NOT USED in blend (handled by static mode overrides)
}
```

### 3.5 The Problem

The current blend rules are simplistic heuristics:

1. **`temporal` category → nudge message_weight up by 0.1 * similarity * blendStrength** — This is too crude. A dopamine temporal hit (fast signaling → weight current message) and a peptide temporal hit (slow signaling → weight history) both increase message_weight. They should push in opposite directions.

2. **`scoring` category → blend toward weighted average of research values** — This is better but the scoring rows currently only have `message_weight` and `freshness_half_life_days` values. The blend only touches `MessageWeight`, ignoring `freshness_half_life_days`.

3. **`drift` category → decrease shift_threshold** — Always decreasing sensitivity makes no sense. If cortisol drift rows are relevant (slow recovery), threshold should increase. If dopamine drift rows are relevant (fast changes), threshold should decrease.

4. **`interaction` category → decrease coherence threshold** — Only one interaction row exists (OT→cortisol attenuation). The blend rule is too generic.

5. **`mode_signal` category → not used at all** — Three rows exist with coherence interpretation codes but nothing reads them during blending.

6. **Many `temporal` rows have no clear parameter mapping** — Dopamine clearance time (200ms) is interesting research but "how does knowing clearance is 200ms change any algorithm parameter?" is unanswered.

---

## 4. The ResolvedAlgorithmConfig — What Can Be Adjusted

```csharp
public record ResolvedAlgorithmConfig(
    ScoringConfig Scoring,
    PoolingConfig Pooling,
    DriftConfig Drift,
    CoherenceConfig Coherence,
    AttractorConfig Attractors,
    ClusteringConfig Clustering,
    SubspaceConfig Subspace);

public record ScoringConfig(
    float MessageWeight,           // 0.1-0.95: current message vs profile history
    float FreshnessBoost,          // multiplier for recent observations
    float MinSimilarity,           // minimum cosine sim to include row
    int TopPerChemical,            // rows to keep per chemical
    float FreshnessHalfLifeDays,   // temporal decay of observation relevance
    float FreshnessFloor,          // minimum freshness (never below this)
    float FreshnessAmplitude);     // range of freshness variation

public record PoolingConfig(
    string Strategy,               // mean|temporal_decay|attentive|attentive_temporal_decay|max|confidence_weighted
    float AttentionTemperature,    // softmax temp (lower = sharper focus on dominant signal)
    int TemporalHalfLife,          // days for temporal decay in pooling
    int MinEntriesForStrategy);    // minimum entries before advanced pooling

public record DriftConfig(
    float ShiftThreshold,          // cosine delta for "real" drift
    float StdDevMultiplier,        // std devs above baseline = significant
    int VelocityWindow,            // recent observations for velocity calc
    float NoiseFloor,              // below this = noise
    float SubspaceDriftWeight);    // weight of per-band drift vs global

public record CoherenceConfig(
    float LowThreshold,            // below this = low cross-layer coherence
    string Interpretation,          // what low coherence means for this context
    float NtHormoneWeight,         // weight of NT-hormone pair
    float NtPeptideWeight,         // weight of NT-peptide pair
    float HormonePeptideWeight);   // weight of hormone-peptide pair

public record AttractorConfig(
    float Threshold,               // cosine sim to classify as attractor revisit
    int MinVisits,                 // minimum visits to declare attractor
    int MaxAttractors,             // maximum tracked attractors
    bool TrackTransitions);        // whether to track transitions between attractors

public record ClusteringConfig(
    float Threshold,               // cosine distance for write-time clustering
    int KNeighbors,                // K for k-NN at read time
    int MinClusterSize);           // minimum cluster members

public record SubspaceConfig(
    int NumBands,                  // number of subspace bands (16)
    int BandDim,                   // dimension per band (256)
    float DivergenceThreshold);    // cross-layer divergence threshold
```

---

## 5. Where Config Is Actually Consumed

### ProfileScoringService (neurorespond read path)
Consumes from `config.Scoring`:
- `MessageWeight` → passed to SQL `get_scored_layer_profile` as relative weight of message embedding vs relationship embedding
- `TopPerChemical` → SQL LIMIT per chemical
- `FreshnessFloor`, `FreshnessAmplitude`, `FreshnessHalfLifeDays` → SQL freshness decay formula

### PersonalityService.WriteProfilesAsync (write path)
Consumes from `config.Clustering`:
- `Threshold` → cosine distance threshold for joining existing cluster vs creating new one

### VectorService.ComputeHeatmapAsync (full scan path)
Consumes from `config.Scoring`:
- `TopPerChemical` → number of top contributors in heatmap

### LayerAnalysis.Compute (neurorespond analysis path)
Consumes from `config.Coherence`:
- `NtHormoneWeight`, `NtPeptideWeight`, `HormonePeptideWeight` → weighted pairwise coherence
- `LowThreshold` → determines CoherenceInterpretation

Consumes from `config.Drift`:
- `ShiftThreshold` → conflict axis detection

Consumes from `config.Subspace`:
- `DivergenceThreshold` → subspace gap detection
- `NumBands`, `BandDim` → subspace band structure

---

## 6. What Needs Revision

### 6.1 Kinetics Row Design
- Should each row map to a specific algorithm parameter it wants to adjust? Or should the category system remain and the blend logic get smarter?
- The `temporal` category has 20+ rows but only a vague "increase message_weight" mapping. These rows are valuable research knowledge but their effect on algorithm parameters is undefined.
- Should new categories be added (e.g., `freshness`, `pooling`, `clustering`)?
- Should `parameter_name` in kinetics rows match `ResolvedAlgorithmConfig` field names directly?

### 6.2 Blend Rules
- The current 4 rules (temporal→message_weight, drift→shift_threshold, scoring→message_weight, interaction→coherence) are too crude
- Each category should map to the specific algorithm parameters it should influence, with directionality
- The blend should consider the chemical identity and layer of the kinetics hit, not just the category

### 6.3 Missing Kinetics Data
- No kinetics rows target `FreshnessHalfLifeDays` blending (only 3 scoring rows have it as parameter_name but blend logic ignores it)
- No kinetics rows target `PoolingConfig` adjustments
- No kinetics rows target `ClusteringConfig` adjustments
- No kinetics rows target `AttractorConfig` adjustments
- No kinetics rows target `SubspaceConfig` adjustments

### 6.4 Blend Strength Semantics
- `blendStrength` is a global scalar (0.0-1.0). Should it be per-category? Per-parameter-group?
- At blendStrength=0.1 the adjustments are tiny (0.1 * 0.8 similarity * 0.1 blend = 0.008 change to message_weight). Is this meaningful?

---

## 7. Constraints

1. **No DB schema changes** — the `neurochemical_kinetics` table schema is fixed. Columns: layer, chemical, category, parameter_name, parameter_value, unit, confidence, description, context, source, related_modes, description_embedding.

2. **Blend must be pure computation** — `ConfigResolver.BlendWithKinetics` is in AnalysisFramework (no DB, no DI). It receives a static config + a list of KineticsHit + blendStrength, returns adjusted config. That's the interface.

3. **Kinetics data can be changed** — the ~36 rows in `KineticsSeedService.GetKineticsData()` are C# code, not user data. They can be rewritten, recategorized, expanded, or restructured.

4. **New categories are fine** — the `category` column is TEXT, not an enum. Any string works.

5. **Must remain research-grounded** — every kinetics row should cite real neurochemistry. No made-up parameters.

6. **blend_strength starts at 0.0** — the system must degrade gracefully. At 0.0, no kinetics blending occurs. At low values (0.05-0.1), adjustments should be subtle. At higher values (0.3-0.5), adjustments should be noticeable but never override the static config entirely.

7. **The KineticsHit record is:** `(string Category, string ParameterName, double ParameterValue, double Similarity)` — this is what BlendWithKinetics receives per retrieved row.

8. **The CategorySignal record is:** `(double AvgSimilarity, double MaxSimilarity, double TotalWeight, int Count, double WeightedAvgValue)` — this is what gets computed per category group.
