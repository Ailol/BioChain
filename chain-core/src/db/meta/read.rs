use spacetimedb::{reducer, ReducerContext};
use crate::db::meta::tables::*;

#[reducer]
pub fn query_meta(
    ctx: &ReducerContext,
    program_id: u64,
    rank_tag: Option<String>,
) {
    let ops: Box<dyn Iterator<Item = MetaOp>> = match &rank_tag {
        Some(r) => Box::new(ctx.db.meta_op().by_rank().filter((program_id, r))),
        None => Box::new(ctx.db.meta_op().by_program().filter(program_id)),
    };
    for m in ops {
        log::info!(
            "@{}|[{}:{}]({}.{}@{}:{})",
            m.rank_tag,
            m.window.kind, m.window.value,
            m.target.code, m.target.property,
            m.target.region, m.target.program
        );
    }
}
