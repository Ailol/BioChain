use spacetimedb::table;
use crate::types::*;

#[table(
    accessor = program,
    public,
    index(accessor = by_patient, btree(columns = [patient_id]))
)]
pub struct Program {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub patient_id: String,
    pub name: String,
    pub phase: Option<String>,
    pub domains: Vec<String>,
    pub tick: u32,
    pub raw_base: Option<String>,
    pub raw_plasticity: Option<String>,
    pub raw_meta: Option<String>,
    pub raw_convergence: Option<String>,
    pub created_at: spacetimedb::Timestamp,
}

#[table(
    accessor = node,
    public,
    index(accessor = by_program, btree(columns = [program_id])),
    index(accessor = by_program_rank, btree(columns = [program_id, rank_tag]))
)]
pub struct Node {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub code: String,
    pub kind: String,         // L.nt | R | K | N.pyr | B.beh | P.agg | etc
    pub region: Option<String>,
    pub rank_tag: String,     // R0 | R1

    pub state: Option<NodeState>,
    pub integ: Option<Integration>,
    pub props: Vec<Kv>,
    pub is_root: bool,
    pub terminal: Option<String>,     // ↺⁺|↺⁻|↺⁰|→⊘|→□|→≋|→Δm with detail
}

#[table(
    accessor = edge,
    public,
    index(accessor = by_program, btree(columns = [program_id])),
    index(accessor = by_source, btree(columns = [program_id, source_id])),
    index(accessor = by_target, btree(columns = [program_id, target_id]))
)]
pub struct Edge {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub source_id: u64,
    pub target_id: u64,
    pub rank_tag: String,     // R0 | R2

    // R0
    pub edge_type: Option<String>,
    pub coeff: f32,

    // R0 gating
    pub gate: Option<GateSpec>,

    // R2 protocol
    pub protocol: Option<ProtocolSpec>,
    pub proto_label: Option<String>,

    // ordering
    pub chain: Option<String>,
    pub chain_pos: Option<u32>,
    pub ring_id: Option<String>,
}

#[table(
    accessor = tensor,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct Tensor {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub conditions: Vec<TensorCond>,
    pub logic: String,        // AND | OR
    pub effect: TensorEffect,
    pub label: Option<String>,
}

#[table(
    accessor = diag,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct Diag {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub kind: String,         // composite | dysreg | observable
    pub name: Option<String>,
    pub expr: String,
    pub detail: Vec<Kv>,
}
