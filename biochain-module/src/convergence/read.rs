use spacetimedb::{reducer, ReducerContext};
use crate::convergence::tables::*;

#[reducer]
pub fn query_conv(
    ctx: &ReducerContext,
    program_id: u64,
    kind: Option<String>,
) {
    for c in ctx.db.conv().by_program().filter(program_id) {
        if let Some(ref k) = kind {
            if &c.kind != k { continue; }
        }
        match c.kind.as_str() {
            "state" => log::info!(
                "\u{222e}({}@{:?})\u{2192}{}",
                c.signal_code.as_deref().unwrap_or("?"),
                c.signal_region,
                c.diagnosis.as_deref().unwrap_or("?")
            ),
            "predict" => log::info!(
                "\u{22b3}({}@{:?},{})\u{003d}{}|{}",
                c.signal_code.as_deref().unwrap_or("?"),
                c.signal_region,
                c.timeframe.as_deref().unwrap_or("?"),
                c.predicted.as_deref().unwrap_or("?"),
                c.rationale.as_deref().unwrap_or("")
            ),
            "flag" => log::info!(
                "\u{26a1}{}:{}",
                c.flag_type.as_deref().unwrap_or("?"),
                c.flag_expr.as_deref().unwrap_or("")
            ),
            _ => {}
        }
    }
}
