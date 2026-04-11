use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::db::meta::tables::*;

#[reducer]
pub fn add_meta(
    ctx: &ReducerContext,
    program_id: u64,
    rank_tag: String,
    window: MetaWindow,
    target: MetaTarget,
) {
    ctx.db.meta_op().insert(MetaOp {
        id: 0, program_id, rank_tag, window, target,
    });
}
