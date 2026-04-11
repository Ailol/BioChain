use spacetimedb::table;
use crate::types::*;

#[table(
    accessor = snapshot,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct Snapshot {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub tick: u32,
    pub label: Option<String>,
    pub created_at: spacetimedb::Timestamp,
}

#[table(
    accessor = snapshot_node,
    public,
    index(accessor = by_snapshot, btree(columns = [snapshot_id]))
)]
pub struct SnapshotNode {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub snapshot_id: u64,
    pub node_id: u64,
    pub code: String,
    pub region: Option<String>,
    pub state_sym: Option<String>,
    pub state_val: Option<f32>,
}

#[table(
    accessor = snapshot_edge,
    public,
    index(accessor = by_snapshot, btree(columns = [snapshot_id]))
)]
pub struct SnapshotEdge {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub snapshot_id: u64,
    pub edge_id: u64,
    pub coeff: f32,
    pub gain: Option<f32>,
}

#[table(
    accessor = sim_run,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct SimRun {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub max_ticks: u32,
    pub perturbations: Vec<Perturbation>,
    pub status: String,       // running | steady_state | timeout | error
    pub final_tick: u32,
    pub started_at: spacetimedb::Timestamp,
}

#[table(
    accessor = sim_tick,
    public,
    index(accessor = by_run, btree(columns = [sim_run_id])),
    index(accessor = by_run_tick, btree(columns = [sim_run_id, tick]))
)]
pub struct SimTick {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub sim_run_id: u64,
    pub tick: u32,
    pub node_id: u64,
    pub value: f32,
    pub delta: f32,
    pub event: String,        // propagate | integrate | protocol | conditional | ring | delta_fire
    pub sources: Vec<u64>,    // edge IDs that contributed
}

#[table(
    accessor = tau_acc,
    public,
    index(accessor = by_run, btree(columns = [sim_run_id]))
)]
pub struct TauAccumulator {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub sim_run_id: u64,
    pub trigger_id: u64,
    pub trigger_kind: String, // edge | protocol | delta
    pub accumulated: f32,     // hours accumulated
    pub tau_target: f32,      // hours needed to fire
}

#[table(
    accessor = diff_result,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct DiffResult {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub snap_a: u64,
    pub snap_b: u64,
    pub diffs: Vec<SnapshotDiff>,
    pub created_at: spacetimedb::Timestamp,
}
