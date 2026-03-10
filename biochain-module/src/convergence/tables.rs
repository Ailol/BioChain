use spacetimedb::table;
use crate::types::*;

#[table(
    accessor = conv,
    public,
    index(accessor = by_program, btree(columns = [program_id]))
)]
pub struct Conv {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub program_id: u64,
    pub kind: String,

    pub signal_code: Option<String>,
    pub signal_region: Option<String>,
    pub vectors: Option<Vec<ConvVector>>,
    pub diagnosis: Option<String>,

    pub timeframe: Option<String>,
    pub predicted: Option<String>,
    pub rationale: Option<String>,

    pub flag_type: Option<String>,
    pub flag_expr: Option<String>,
}
