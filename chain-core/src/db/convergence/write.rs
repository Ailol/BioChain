use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::db::convergence::tables::*;

#[reducer]
pub fn add_conv(
    ctx: &ReducerContext,
    program_id: u64,
    kind: String,
    signal_code: Option<String>,
    signal_region: Option<String>,
    vectors: Option<Vec<ConvVector>>,
    diagnosis: Option<String>,
    timeframe: Option<String>,
    predicted: Option<String>,
    rationale: Option<String>,
    confidence: Option<String>,
    flag_type: Option<String>,
    flag_expr: Option<String>,
    risk_name: Option<String>,
    risk_target: Option<String>,
    risk_distance: Option<String>,
    risk_window: Option<String>,
    risk_reversible_before: Option<String>,
    risk_reversible_after: Option<String>,
    monitor_measurement: Option<String>,
    monitor_flag_ref: Option<String>,
    monitor_note: Option<String>,
) {
    ctx.db.conv().insert(Conv {
        id: 0, program_id, kind,
        signal_code, signal_region, vectors, diagnosis,
        timeframe, predicted, rationale, confidence,
        flag_type, flag_expr,
        risk_name, risk_target, risk_distance,
        risk_window, risk_reversible_before, risk_reversible_after,
        monitor_measurement, monitor_flag_ref, monitor_note,
    });
}
