use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::plasticity::tables::*;

#[reducer]
pub fn add_delta(
    ctx: &ReducerContext,
    program_id: u64,
    rank_tag: String,
    trigger_code: String,
    trigger_region: String,
    trigger_state: String,
    target_code: String,
    target_region: String,
    change: PropChange,
    tau: String,
    depends: Vec<String>,
    status: Option<String>,
    cascade_name: Option<String>,
    tensor_expr: Option<String>,
) {
    ctx.db.delta_op().insert(DeltaOp {
        id: 0, program_id, rank_tag,
        trigger_code, trigger_region, trigger_state,
        target_code, target_region, change,
        tau, depends, status, cascade_name, tensor_expr,
    });
}

#[reducer]
pub fn log_delta_fired(
    ctx: &ReducerContext,
    program_id: u64,
    delta_op_id: u64,
    tick: u32,
) {
    ctx.db.delta_log().insert(DeltaLog {
        id: 0, program_id, delta_op_id,
        fired_at_tick: tick,
        fired_at: ctx.timestamp,
    });
}
