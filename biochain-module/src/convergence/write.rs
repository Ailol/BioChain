use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::convergence::tables::*;

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
    flag_type: Option<String>,
    flag_expr: Option<String>,
) {
    ctx.db.conv().insert(Conv {
        id: 0, program_id, kind,
        signal_code, signal_region, vectors, diagnosis,
        timeframe, predicted, rationale,
        flag_type, flag_expr,
    });
}
