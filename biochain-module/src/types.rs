use spacetimedb::SpacetimeType;

// -- Node state (R0) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct NodeState {
    pub sym: String,          // ↑↑ | ↑ | ≈ | ↓ | ↓↓ | ~ | ⊘ | ●
    pub val: Option<f32>,
    pub delta_sign: Option<String>,
    pub delta_val: Option<f32>,
}

// -- Integration (R1) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct IntegInput {
    pub code: String,         // GLU, GABA, CORT
    pub region: String,       // VTA, DRN, ADR
    pub weight: f32,          // +0.7, -0.5
    pub w_type: String,       // exc | inh | mod
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct IntegOutput {
    pub code: String,         // DA, 5HT, GLU
    pub region: String,
    pub mode: String,         // thr | rate | burst | tonic
    pub threshold: Option<String>,
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct Integration {
    pub inputs: Vec<IntegInput>,
    pub output: IntegOutput,
}

// -- Protocol (R2) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct ProtocolSpec {
    pub gain: Option<f32>,
    pub polarity: Option<String>,
    pub tau_class: Option<String>,
    pub tau_value: Option<String>,
    pub gate: Option<String>,
    pub coupling: Option<String>,
    pub release_pr: Option<f32>,
}

// -- Edge gating --

#[derive(SpacetimeType, Clone, Debug)]
pub struct GateSpec {
    pub node_code: String,
    pub region: String,
    pub threshold: String,    // >=↑
}

// -- Tensor (R3) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct TensorCond {
    pub code: String,
    pub region: String,
    pub state: String,        // ↑ | ↑↑ | ≈ | ↓ | ↓↓
    pub negated: bool,
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct TensorEffect {
    pub code: String,
    pub region: String,
    pub action: String,       // pass | block | amplify | switch
    pub value: Option<f32>,
    pub switch_to: Option<String>,
}

// -- Plasticity (Δ) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct PropChange {
    pub property: String,     // release | baseline | gain | spines
    pub before: String,       // norm | open | ×1.0
    pub after: String,        // depleted | desens | ×1.5
}

// -- Meta --

#[derive(SpacetimeType, Clone, Debug)]
pub struct MetaWindow {
    pub kind: String,         // age_range | condition | cumulative
    pub value: String,        // "0yr-5yr" | "after:CORT.chronic:6mo"
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct MetaTarget {
    pub code: String,
    pub region: String,
    pub property: String,
    pub program: String,      // "norm→low" | "plastic" | "methylation_locked"
}

// -- Convergence --

#[derive(SpacetimeType, Clone, Debug)]
pub struct ConvVector {
    pub source: String,       // v_past | v_current | v_meta
    pub state: String,        // ↑ | ↓ | ↓↓
    pub detail: Option<String>,
}

// -- Generic key-value --

#[derive(SpacetimeType, Clone, Debug)]
pub struct Kv {
    pub k: String,
    pub v: String,
}
