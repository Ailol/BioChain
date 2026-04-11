use spacetimedb::{reducer, ReducerContext};
use crate::db::plasticity::tables::*;

#[reducer]
pub fn query_deltas(
    ctx: &ReducerContext,
    program_id: u64,
    rank_tag: Option<String>,
) {
    let ops: Box<dyn Iterator<Item = DeltaOp>> = match &rank_tag {
        Some(r) => Box::new(ctx.db.delta_op().by_rank().filter((program_id, r))),
        None => Box::new(ctx.db.delta_op().by_program().filter(program_id)),
    };
    for d in ops {
        log::info!(
            "\u{0394}@{}|{}@{}[{}]\u{226b}{}@{}({}:{}->{})[τ:{}]",
            d.rank_tag,
            d.trigger_code, d.trigger_region, d.trigger_state,
            d.target_code, d.target_region,
            d.change.property, d.change.before, d.change.after,
            d.tau
        );
    }
}

#[reducer]
pub fn query_delta_log(ctx: &ReducerContext, program_id: u64) {
    for l in ctx.db.delta_log().by_program().filter(program_id) {
        log::info!("FIRED|op:{}|tick:{}|at:{:?}",
            l.delta_op_id, l.fired_at_tick, l.fired_at);
    }
}
