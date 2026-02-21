# MBTI Layered Prototype Generator — Implementation Plan

## Goal
Replace hardcoded `MbtiPrototypes.ChemicalDescriptions` with a 4-layer inference pipeline
that generates the same `Dictionary<(string Type, string Chemical), string>` output.
The existing `MbtiClassifier`, `MbtiService`, and embedding flow remain untouched.

## Architecture: 4-Layer Pipeline

### Layer 1: Cognitive Functions → Dimension Weights
**New file:** `NeuroGateway.AnalysisFramework/Mbti/CognitiveFunctions.cs`

Define 8 cognitive functions as weighted profiles over the 24 existing dimensions.
Define 16 type → function stack mappings with position weights.

### Layer 2: Dimension Weights × DB Affinities → Chemical Profiles
**New file:** `NeuroGateway.AnalysisFramework/Mbti/ChemicalProfileBuilder.cs`

Multiply function dimension weights through `dimension_chemical_affinity` to get
per-function chemical intensity profiles.

### Layer 3: Function Stack × Chemical Interactions → Type Chemical Profile
Compose 4-function stacks with position weighting, apply `chemical_interaction`
mod_factors for cross-chemical effects.

### Layer 4: Type Chemical Profile → Description Text
**New file:** `NeuroGateway.AnalysisFramework/Mbti/PrototypeTextGenerator.cs`

Per-chemical text templates inflected by intensity, function attitude, and interactions.
Outputs the final `Dictionary<(string Type, string Chemical), string>`.

## Files to Create/Modify
1. **CREATE** `CognitiveFunctions.cs` — Layer 1
2. **CREATE** `ChemicalProfileBuilder.cs` — Layers 2+3
3. **CREATE** `PrototypeTextGenerator.cs` — Layer 4
4. **MODIFY** `MbtiPrototypes.cs` — Computed property replaces hardcoded dict
5. **MODIFY** `MbtiService.cs` — Load DB data, pass to pipeline, bump version

## What Does NOT Change
- `MbtiClassifier.cs` — same dictionary input
- Frontend — no changes
- DB schema — read-only from existing tables
- `EmbeddingApi` / re-embed flow — unchanged
