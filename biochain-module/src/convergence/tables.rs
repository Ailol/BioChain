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
    pub kind: String,             // conv_state | trajectory | risk | flag | monitor

    pub signal_code: Option<String>,
    pub signal_region: Option<String>,
    pub vectors: Option<Vec<ConvVector>>,
    pub diagnosis: Option<String>,

    pub timeframe: Option<String>,
    pub predicted: Option<String>,
    pub rationale: Option<String>,
    pub confidence: Option<String>, // high | moderate | low

    pub flag_type: Option<String>,
    pub flag_expr: Option<String>,

    // risk fields
    pub risk_name: Option<String>,
    pub risk_target: Option<String>,
    pub risk_distance: Option<String>,   // close | moderate | distant
    pub risk_window: Option<String>,
    pub risk_reversible_before: Option<String>,
    pub risk_reversible_after: Option<String>,

    // monitor fields
    pub monitor_measurement: Option<String>,
    pub monitor_flag_ref: Option<String>,
    pub monitor_note: Option<String>,
}
