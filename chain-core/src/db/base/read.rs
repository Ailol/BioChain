use spacetimedb::{reducer, ReducerContext};
use crate::db::base::tables::*;

#[reducer]
pub fn query_node(
    ctx: &ReducerContext,
    program_id: u64,
    code: String,
    region: Option<String>,
) {
    for n in ctx.db.node().by_program().filter(program_id)
        .filter(|n| n.code == code)
        .filter(|n| region.is_none() || n.region == region)
    {
        log::info!(
            "NODE|{}|{}|{:?}|{}|{:?}|root:{}",
            n.code, n.kind, n.region, n.rank_tag,
            n.state.as_ref().map(|s| &s.sym), n.is_root
        );
    }
}

#[reducer]
pub fn query_graph(ctx: &ReducerContext, program_id: u64, rank_tag: String) {
    for n in ctx.db.node().by_program_rank().filter((program_id, &rank_tag)) {
        log::info!("N|{}|{}@{:?}|{:?}", n.id, n.code, n.region,
            n.state.as_ref().map(|s| &s.sym));
    }
    for e in ctx.db.edge().by_program().filter(program_id)
        .filter(|e| e.rank_tag == rank_tag)
    {
        log::info!("E|{}→{}|{:?}", e.source_id, e.target_id, e.edge_type);
    }
}

#[reducer]
pub fn query_edges_from(
    ctx: &ReducerContext,
    program_id: u64,
    source_id: u64,
) {
    for e in ctx.db.edge().by_source().filter((program_id, source_id)) {
        log::info!(
            "EDGE|{}|{}→{}|{}|{:?}|coeff:{}",
            e.id, e.source_id, e.target_id,
            e.rank_tag, e.edge_type, e.coeff
        );
    }
}

#[reducer]
pub fn query_tensors(ctx: &ReducerContext, program_id: u64) {
    for t in ctx.db.tensor().by_program().filter(program_id) {
        let conds: Vec<String> = t.conditions.iter().map(|c|
            format!("{}{}@{}>={}",
                if c.negated { "\u{00ac}" } else { "" },
                c.code, c.region, c.state)
        ).collect();
        log::info!("T|{}\u{27f9}{}@{}:{}:{:?}",
            conds.join(" \u{2227} "),
            t.effect.code, t.effect.region,
            t.effect.action, t.effect.value);
    }
}
