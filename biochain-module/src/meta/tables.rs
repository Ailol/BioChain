use spacetimedb::table;
use crate::types::*;

#[table(
    accessor = meta_op,
    public,
    index(accessor = by_program, btree(columns = [program_id])),
    index(accessor = by_rank, btree(columns = [program_id, rank_tag]))
)]
pub struct MetaOp {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub rank_tag: String,

    pub window: MetaWindow,
    pub target: MetaTarget,
}
