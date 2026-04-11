use spacetimedb::SpacetimeType;

// -- Node state (R0) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct NodeState {
    pub sym: String,          // ++ | + | = | ~ | - | -- | X | *
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
    pub threshold: String,    // >=+
}

// -- Tensor (R3) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct TensorCond {
    pub code: String,
    pub region: String,
    pub state: String,        // ++ | + | = | ~ | - | --
    pub negated: bool,
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct TensorEffect {
    pub code: String,
    pub region: String,
    pub action: String,       // pass | block | amplify | switch | apoptosis
    pub value: Option<f32>,
    pub switch_to: Option<String>,
}

// -- Plasticity (Δ) --

#[derive(SpacetimeType, Clone, Debug)]
pub struct PropChange {
    pub property: String,     // Δ0: release|baseline|synthesis|reuptake|secretion|pool|conversion|aggregation
                              // Δ1: spines|dendrite|axon|myelin|state|volume|neurogenesis|permeability|motility|innervation|pool_capacity|receptor_density|neuron_count
                              // Δ2: gain|gate|tau|pr|dens|st|coup
    pub before: String,       // norm | open | full | functional | voluntary | situational
    pub after: String,        // depleted | desens | reduced | X | compulsive | generalized
}

// -- Meta --

#[derive(SpacetimeType, Clone, Debug)]
pub struct MetaWindow {
    pub kind: String,         // age_range | condition | cumulative | congenital | aging
    pub value: String,        // "0yr-5yr" | "after:CORT.chronic:6mo" | "0yr-∞" | "60yr-∞"
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct MetaTarget {
    pub code: String,
    pub region: String,
    pub property: String,
    pub program: String,           // "norm→low" | "plastic" | "methylation_locked"
    pub reversible: Option<String>,// yes | difficult | no
    pub unlocks_with: Option<String>,
    pub pull: Option<String>,      // weak | moderate | strong
}

// -- Convergence --

#[derive(SpacetimeType, Clone, Debug)]
pub struct ConvVector {
    pub source: String,       // v_past | v_current | v_meta
    pub state: String,        // ++ | + | = | ~ | - | --
    pub detail: Option<String>,
}

// -- Generic key-value --

#[derive(SpacetimeType, Clone, Debug)]
pub struct Kv {
    pub k: String,
    pub v: String,
}

// -- Simulation --

#[derive(SpacetimeType, Clone, Debug)]
pub struct Perturbation {
    pub target_code: String,
    pub target_region: String,
    pub action: String,       // set | add | block
    pub value: Option<f32>,
}

#[derive(SpacetimeType, Clone, Debug)]
pub struct SnapshotDiff {
    pub kind: String,         // node_added | node_removed | node_changed | edge_added | edge_removed | edge_changed
    pub entity_id: u64,
    pub field: String,
    pub old_val: String,
    pub new_val: String,
}
