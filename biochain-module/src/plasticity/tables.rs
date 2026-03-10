use spacetimedb::table;
use crate::types::*;

#[table(
    accessor = delta_op,
    public,
    index(accessor = by_program, btree(columns = [program_id])),
    index(accessor = by_rank, btree(columns = [program_id, rank_tag]))
)]
pub struct DeltaOp {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub rank_tag: String,

    pub trigger_code: String,
    pub trigger_region: String,
    pub trigger_state: String,

    pub target_code: String,
    pub target_region: String,
    pub change: PropChange,

    pub tau: String,
    pub tensor_expr: Option<String>,
}

#[table(
    accessor = delta_log,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct DeltaLog {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub delta_op_id: u64,
    pub fired_at_tick: u32,
    pub fired_at: spacetimedb::Timestamp,
}
