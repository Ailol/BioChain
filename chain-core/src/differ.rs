use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::db::sim::tables::*;
use std::collections::HashMap;

// ═══════════════════════════════════════════════════════════════════
// Snapshot differ: compare two snapshots, produce change set
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn diff_snapshots(
    ctx: &ReducerContext,
    program_id: u64,
    snap_a_id: u64,
    snap_b_id: u64,
) -> Result<(), String> {
    // verify both snapshots exist and belong to program
    let snap_a = ctx.db.snapshot().id().find(snap_a_id)
        .ok_or("Snapshot A not found")?;
    let snap_b = ctx.db.snapshot().id().find(snap_b_id)
        .ok_or("Snapshot B not found")?;
    if snap_a.program_id != program_id || snap_b.program_id != program_id {
        return Err("Snapshots don't belong to this program".to_string());
    }

    let mut diffs = Vec::new();

    // ─── Node diffs ───
    let nodes_a: Vec<SnapshotNode> = ctx.db.snapshot_node()
        .by_snapshot().filter(snap_a_id).collect();
    let nodes_b: Vec<SnapshotNode> = ctx.db.snapshot_node()
        .by_snapshot().filter(snap_b_id).collect();

    let map_a: HashMap<u64, &SnapshotNode> = nodes_a.iter()
        .map(|n| (n.node_id, n)).collect();
    let map_b: HashMap<u64, &SnapshotNode> = nodes_b.iter()
        .map(|n| (n.node_id, n)).collect();

    // nodes in A but not in B → removed
    for (&nid, na) in &map_a {
        if !map_b.contains_key(&nid) {
            diffs.push(SnapshotDiff {
                kind: "node_removed".to_string(),
                entity_id: nid,
                field: "code".to_string(),
                old_val: format!("{}@{}", na.code, na.region.as_deref().unwrap_or("")),
                new_val: String::new(),
            });
        }
    }

    // nodes in B but not in A → added
    for (&nid, nb) in &map_b {
        if !map_a.contains_key(&nid) {
            diffs.push(SnapshotDiff {
                kind: "node_added".to_string(),
                entity_id: nid,
                field: "code".to_string(),
                old_val: String::new(),
                new_val: format!("{}@{}", nb.code, nb.region.as_deref().unwrap_or("")),
            });
        }
    }

    // nodes in both → check for state changes
    for (&nid, na) in &map_a {
        if let Some(nb) = map_b.get(&nid) {
            // state_sym change
            if na.state_sym != nb.state_sym {
                diffs.push(SnapshotDiff {
                    kind: "node_changed".to_string(),
                    entity_id: nid,
                    field: "state_sym".to_string(),
                    old_val: na.state_sym.clone().unwrap_or_default(),
                    new_val: nb.state_sym.clone().unwrap_or_default(),
                });
            }
            // state_val change
            let va = na.state_val.unwrap_or(0.0);
            let vb = nb.state_val.unwrap_or(0.0);
            if (va - vb).abs() > 1e-4 {
                diffs.push(SnapshotDiff {
                    kind: "node_changed".to_string(),
                    entity_id: nid,
                    field: "state_val".to_string(),
                    old_val: format!("{:.4}", va),
                    new_val: format!("{:.4}", vb),
                });
            }
        }
    }

    // ─── Edge diffs ───
    let edges_a: Vec<SnapshotEdge> = ctx.db.snapshot_edge()
        .by_snapshot().filter(snap_a_id).collect();
    let edges_b: Vec<SnapshotEdge> = ctx.db.snapshot_edge()
        .by_snapshot().filter(snap_b_id).collect();

    let emap_a: HashMap<u64, &SnapshotEdge> = edges_a.iter()
        .map(|e| (e.edge_id, e)).collect();
    let emap_b: HashMap<u64, &SnapshotEdge> = edges_b.iter()
        .map(|e| (e.edge_id, e)).collect();

    // edges removed
    for (&eid, _) in &emap_a {
        if !emap_b.contains_key(&eid) {
            diffs.push(SnapshotDiff {
                kind: "edge_removed".to_string(),
                entity_id: eid,
                field: "edge".to_string(),
                old_val: format!("edge_{}", eid),
                new_val: String::new(),
            });
        }
    }

    // edges added
    for (&eid, _) in &emap_b {
        if !emap_a.contains_key(&eid) {
            diffs.push(SnapshotDiff {
                kind: "edge_added".to_string(),
                entity_id: eid,
                field: "edge".to_string(),
                old_val: String::new(),
                new_val: format!("edge_{}", eid),
            });
        }
    }

    // edges changed (coeff or gain)
    for (&eid, ea) in &emap_a {
        if let Some(eb) = emap_b.get(&eid) {
            if (ea.coeff - eb.coeff).abs() > 1e-4 {
                diffs.push(SnapshotDiff {
                    kind: "edge_changed".to_string(),
                    entity_id: eid,
                    field: "coeff".to_string(),
                    old_val: format!("{:.4}", ea.coeff),
                    new_val: format!("{:.4}", eb.coeff),
                });
            }
            let ga = ea.gain.unwrap_or(1.0);
            let gb = eb.gain.unwrap_or(1.0);
            if (ga - gb).abs() > 1e-4 {
                diffs.push(SnapshotDiff {
                    kind: "edge_changed".to_string(),
                    entity_id: eid,
                    field: "gain".to_string(),
                    old_val: format!("{:.4}", ga),
                    new_val: format!("{:.4}", gb),
                });
            }
        }
    }

    // ─── Store result ───
    let diff_count = diffs.len();
    ctx.db.diff_result().insert(DiffResult {
        id: 0,
        program_id,
        snap_a: snap_a_id,
        snap_b: snap_b_id,
        diffs,
        created_at: ctx.timestamp,
    });

    log::info!("DIFF|snap_a:{}|snap_b:{}|changes:{}", snap_a_id, snap_b_id, diff_count);
    Ok(())
}
