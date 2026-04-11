use spacetimedb::{reducer, ReducerContext};
use crate::sim::tables::*;

#[reducer]
pub fn query_snapshots(ctx: &ReducerContext, program_id: u64) {
    for s in ctx.db.snapshot().by_program().filter(program_id) {
        log::info!("SNAP|id:{}|tick:{}|label:{:?}",
            s.id, s.tick, s.label);
    }
}

#[reducer]
pub fn query_sim_runs(ctx: &ReducerContext, program_id: u64) {
    for r in ctx.db.sim_run().by_program().filter(program_id) {
        let targets: Vec<String> = r.perturbations.iter()
            .map(|p| format!("{}@{}", p.target_code, p.target_region))
            .collect();
        log::info!("RUN|id:{}|status:{}|ticks:{}|targets:{}",
            r.id, r.status, r.final_tick, targets.join(","));
    }
}

#[reducer]
pub fn query_sim_trace(ctx: &ReducerContext, sim_run_id: u64, tick: Option<u32>) {
    for t in ctx.db.sim_tick().by_run().filter(sim_run_id) {
        if let Some(target_tick) = tick {
            if t.tick != target_tick { continue; }
        }
        log::info!("TICK|{}|node:{}|val:{:.4}|Δ:{:.4}|evt:{}|src:{:?}",
            t.tick, t.node_id, t.value, t.delta, t.event, t.sources);
    }
}

#[reducer]
pub fn query_diffs(ctx: &ReducerContext, program_id: u64) {
    for d in ctx.db.diff_result().by_program().filter(program_id) {
        log::info!("DIFF|id:{}|snap_a:{}|snap_b:{}|changes:{}",
            d.id, d.snap_a, d.snap_b, d.diffs.len());
        for diff in &d.diffs {
            log::info!("  {}|id:{}|{}:{}→{}",
                diff.kind, diff.entity_id, diff.field, diff.old_val, diff.new_val);
        }
    }
}
