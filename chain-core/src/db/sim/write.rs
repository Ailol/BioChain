use spacetimedb::{reducer, ReducerContext, Table};
use crate::db::sim::tables::*;
use crate::db::base::tables::*;

#[reducer]
pub fn take_snapshot(
    ctx: &ReducerContext,
    program_id: u64,
    label: Option<String>,
) -> Result<(), String> {
    let p = ctx.db.program().id().find(program_id)
        .ok_or("Program not found")?;

    let snap = ctx.db.snapshot().insert(Snapshot {
        id: 0,
        program_id,
        tick: p.tick,
        label,
        created_at: ctx.timestamp,
    });

    for n in ctx.db.node().by_program().filter(program_id) {
        ctx.db.snapshot_node().insert(SnapshotNode {
            id: 0,
            snapshot_id: snap.id,
            node_id: n.id,
            code: n.code.clone(),
            region: n.region.clone(),
            state_sym: n.state.as_ref().map(|s| s.sym.clone()),
            state_val: n.state.as_ref().and_then(|s| s.val),
        });
    }

    for e in ctx.db.edge().by_program().filter(program_id) {
        ctx.db.snapshot_edge().insert(SnapshotEdge {
            id: 0,
            snapshot_id: snap.id,
            edge_id: e.id,
            coeff: e.coeff,
            gain: e.protocol.as_ref().and_then(|p| p.gain),
        });
    }

    log::info!("SNAPSHOT|id:{}|tick:{}|nodes:{}",
        snap.id, snap.tick,
        ctx.db.snapshot_node().by_snapshot().filter(snap.id).count());
    Ok(())
}
