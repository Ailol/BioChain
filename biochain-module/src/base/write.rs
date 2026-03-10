use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::base::tables::*;

#[reducer]
pub fn create_program(
    ctx: &ReducerContext,
    name: String,
    phase: Option<String>,
    domains: Vec<String>,
) {
    ctx.db.program().insert(Program {
        id: 0, name, phase, domains,
        tick: 0,
        raw_base: None, raw_plasticity: None,
        raw_meta: None, raw_convergence: None,
        created_at: ctx.timestamp,
    });
}

#[reducer]
pub fn store_raw_bnf(
    ctx: &ReducerContext,
    program_id: u64,
    pipeline: String,
    raw: String,
) -> Result<(), String> {
    let mut p = ctx.db.program().id().find(program_id)
        .ok_or("Program not found")?;
    match pipeline.as_str() {
        "base" => p.raw_base = Some(raw),
        "plasticity" => p.raw_plasticity = Some(raw),
        "meta" => p.raw_meta = Some(raw),
        "convergence" => p.raw_convergence = Some(raw),
        _ => return Err(format!("Unknown pipeline: {}", pipeline)),
    }
    ctx.db.program().id().update(p);
    Ok(())
}

#[reducer]
pub fn add_node(
    ctx: &ReducerContext,
    program_id: u64,
    code: String,
    kind: String,
    region: Option<String>,
    rank_tag: String,
    state: Option<NodeState>,
    integ: Option<Integration>,
    field_ops: Vec<String>,
    props: Vec<Kv>,
    is_root: bool,
) {
    ctx.db.node().insert(Node {
        id: 0, program_id, code, kind, region, rank_tag,
        state, integ, field_ops, props, is_root,
    });
}

#[reducer]
pub fn add_edge(
    ctx: &ReducerContext,
    program_id: u64,
    source_id: u64,
    target_id: u64,
    rank_tag: String,
    edge_type: Option<String>,
    coeff: f32,
    gate: Option<GateSpec>,
    protocol: Option<ProtocolSpec>,
    proto_label: Option<String>,
    chain: Option<String>,
    chain_pos: Option<u32>,
    ring_id: Option<String>,
) {
    ctx.db.edge().insert(Edge {
        id: 0, program_id, source_id, target_id, rank_tag,
        edge_type, coeff, gate, protocol, proto_label,
        chain, chain_pos, ring_id,
    });
}

#[reducer]
pub fn add_tensor(
    ctx: &ReducerContext,
    program_id: u64,
    conditions: Vec<TensorCond>,
    logic: String,
    effect: TensorEffect,
    label: Option<String>,
) {
    ctx.db.tensor().insert(Tensor {
        id: 0, program_id, conditions, logic, effect, label,
    });
}

#[reducer]
pub fn add_diag(
    ctx: &ReducerContext,
    program_id: u64,
    kind: String,
    name: Option<String>,
    expr: String,
    detail: Vec<Kv>,
) {
    ctx.db.diag().insert(Diag {
        id: 0, program_id, kind, name, expr, detail,
    });
}
