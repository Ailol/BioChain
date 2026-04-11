use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::db::base::tables::*;
use crate::db::plasticity::tables::*;
use crate::db::meta::tables::*;
use crate::db::convergence::tables::*;
use crate::parser::parse_tau_to_hours;
use std::collections::HashMap;

// ═══════════════════════════════════════════════════════════════════
// Three-vector convergence computation (deterministic, no LLM)
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
struct SignalVectors {
    code: String,
    region: String,
    v_past: VectorInfo,
    v_current: VectorInfo,
    v_meta: VectorInfo,
}

#[derive(Clone, Debug)]
struct VectorInfo {
    direction: f32,    // positive = up, negative = down
    detail: String,
}

// ═══════════════════════════════════════════════════════════════════
// Diagnosis from three-vector agreement
// ═══════════════════════════════════════════════════════════════════

fn diagnose(v: &SignalVectors) -> String {
    let dp = v.v_past.direction;
    let dc = v.v_current.direction;
    let dm = v.v_meta.direction;

    let same_sign = |a: f32, b: f32| -> bool {
        (a > 0.0 && b > 0.0) || (a < 0.0 && b < 0.0) || (a.abs() < 0.1 && b.abs() < 0.1)
    };

    let all_agree = same_sign(dp, dc) && same_sign(dc, dm);
    let past_current_agree = same_sign(dp, dc);
    let past_meta_agree = same_sign(dp, dm);
    let current_meta_agree = same_sign(dc, dm);

    if all_agree {
        if dp > 0.1 { return "converging_high".to_string(); }
        if dp < -0.1 { return "converging_low".to_string(); }
        return "converging_norm".to_string();
    }

    // v_past and v_current overwhelming v_meta
    if past_current_agree && !current_meta_agree && dp.abs() > dm.abs() * 2.0 {
        return "breaking".to_string();
    }

    // v_meta immovable (strong setpoint, others disagree)
    if dm.abs() > dp.abs() && dm.abs() > dc.abs() && !past_meta_agree {
        return "locked".to_string();
    }

    // v_current opposes v_past or v_meta
    if !past_current_agree && !current_meta_agree {
        return "divergent".to_string();
    }

    // v_past and v_meta disagree, v_current caught
    if !past_meta_agree && (current_meta_agree || past_current_agree) {
        return "contested".to_string();
    }

    // degenerating: all vectors trending negative with increasing magnitude
    if dp < -0.1 && dc < -0.1 && dm < -0.1 && dc < dp {
        return "degenerating".to_string();
    }

    // all three disagree
    if !past_current_agree && !past_meta_agree && !current_meta_agree {
        return "unstable".to_string();
    }

    "contested".to_string()
}

// ═══════════════════════════════════════════════════════════════════
// Compute v_past: from Δ0 baseline trends (delta_log history)
// ═══════════════════════════════════════════════════════════════════

fn compute_v_past(
    code: &str,
    region: &str,
    deltas: &[DeltaOp],
    delta_logs: &[DeltaLog],
) -> VectorInfo {
    // find Δ0 rules targeting this signal
    let relevant: Vec<&DeltaOp> = deltas.iter()
        .filter(|d| d.rank_tag == "Δ0" && d.target_code == code && d.target_region == region)
        .collect();

    if relevant.is_empty() {
        // check if this signal IS a trigger (directional pressure)
        let as_trigger: Vec<&DeltaOp> = deltas.iter()
            .filter(|d| d.rank_tag == "Δ0" && d.trigger_code == code && d.trigger_region == region)
            .collect();
        if as_trigger.is_empty() {
            return VectorInfo { direction: 0.0, detail: "no_delta_history".to_string() };
        }
        // direction from trigger state
        let dir = as_trigger.iter().map(|d| sym_direction(&d.trigger_state)).sum::<f32>()
            / as_trigger.len() as f32;
        return VectorInfo {
            direction: dir,
            detail: format!("trigger_trend:{:.2}", dir),
        };
    }

    // compute drift from plasticity effects
    let mut drift = 0.0f32;
    for d in &relevant {
        let effect_dir = match d.change.after.as_str() {
            // State arrow values
            "++" => 2.0, "+" => 1.0, "--" => -2.0, "-" => -1.0, "=" | "~" | "X" => 0.0,
            // Legacy word values
            "depleted" | "low" | "lower" | "reduced" | "down" | "desens" | "retracted" => -1.0,
            "enhanced" | "high" | "up" | "elevated" | "rising" | "increased" | "expanded" => 1.0,
            _ => 0.0,
        };
        // weight by how many times this delta fired
        let fire_count = delta_logs.iter()
            .filter(|l| l.delta_op_id == d.id)
            .count() as f32;
        drift += effect_dir * (fire_count + 1.0).ln().max(0.1);
    }

    let direction = drift.clamp(-2.0, 2.0);
    VectorInfo {
        direction,
        detail: format!("drift:{:.3}/wk", direction * 0.15),
    }
}

fn sym_direction(sym: &str) -> f32 {
    match sym {
        "++" => 2.0,
        "+" => 1.0,
        "=" => 0.0,
        "-" => -1.0,
        "--" => -2.0,
        "~" => 0.0,   // oscillating
        "X" => 0.0,   // inactive
        "*" => 1.0,   // constitutively active
        // legacy unicode
        "↑↑" => 2.0, "↑" => 1.0, "≈" => 0.0, "↓" => -1.0, "↓↓" => -2.0,
        _ => 0.0,
    }
}

// ═══════════════════════════════════════════════════════════════════
// Compute v_current: from ∫ integration output (current state)
// ═══════════════════════════════════════════════════════════════════

fn compute_v_current(
    code: &str,
    region: &str,
    nodes: &[Node],
) -> VectorInfo {
    // find the node's current state
    let node = nodes.iter().find(|n| n.code == code && n.region.as_deref() == Some(region));
    match node {
        Some(n) => {
            let val = n.state.as_ref().map_or(0.0, |s| {
                s.val.unwrap_or_else(|| sym_direction(&s.sym))
            });

            // check if this node has integration (R1)
            let detail = if n.integ.is_some() {
                format!("∫_output:{:.2}", val)
            } else {
                format!("chain_state:{:.2}", val)
            };

            VectorInfo { direction: val, detail }
        }
        None => VectorInfo { direction: 0.0, detail: "not_found".to_string() },
    }
}

// ═══════════════════════════════════════════════════════════════════
// Compute v_meta: from σ̃ setpoints (MetaOp with rank M0)
// ═══════════════════════════════════════════════════════════════════

fn compute_v_meta(
    code: &str,
    region: &str,
    meta_ops: &[MetaOp],
) -> VectorInfo {
    // find σ̃ entries (M0) for this signal
    let setpoints: Vec<&MetaOp> = meta_ops.iter()
        .filter(|m| m.rank_tag == "M0" && m.target.code == code && m.target.region == region)
        .collect();

    if setpoints.is_empty() {
        return VectorInfo { direction: 0.0, detail: "no_setpoint".to_string() };
    }

    // extract setpoint direction from program field
    let sp = &setpoints[0];
    let direction = match sp.target.program.as_str() {
        // State arrow transitions (new format: =→+, =→--, etc.)
        s if s.ends_with("→++") => 2.0,
        s if s.ends_with("→+") && !s.ends_with("→++") => 1.0,
        s if s.ends_with("→--") => -2.0,
        s if s.ends_with("→-") && !s.ends_with("→--") => -1.0,
        s if s.ends_with("→=") => 0.0,
        // Legacy word-based transitions
        s if s.contains("low") || s.contains("reduced") || s.contains("depleted") => -1.0,
        s if s.contains("elevated") || s.contains("high") || s.contains("increased") => 1.0,
        s if s.contains("norm") => 0.0,
        _ => 0.0,
    };

    VectorInfo {
        direction,
        detail: format!("σ̃{}", sp.target.program),
    }
}

// ═══════════════════════════════════════════════════════════════════
// Direction to arrow symbol
// ═══════════════════════════════════════════════════════════════════

fn dir_to_sym(d: f32) -> String {
    if d >= 1.5 { "++".to_string() }
    else if d >= 0.5 { "+".to_string() }
    else if d > -0.5 { "=".to_string() }
    else if d > -1.5 { "-".to_string() }
    else { "--".to_string() }
}

// ═══════════════════════════════════════════════════════════════════
// Trajectory prediction
// ═══════════════════════════════════════════════════════════════════

fn predict_trajectory(
    vectors: &SignalVectors,
    timeframe_hours: f32,
    deltas: &[DeltaOp],
    _delta_logs: &[DeltaLog],
) -> (String, String) {
    let dp = vectors.v_past.direction;
    let dc = vectors.v_current.direction;
    let dm = vectors.v_meta.direction;

    // weighted average: current has most weight, meta acts as attractor
    let momentum = dp * 0.3;
    let drive = dc * 0.5;
    let attractor = dm * 0.2;
    let predicted = (momentum + drive + attractor).clamp(-2.0, 2.0);

    let mut rationale_parts = Vec::new();
    if dm.abs() > 0.1 {
        rationale_parts.push(format!("attractor:{}", if dm > 0.0 { "high" } else { "low" }));
    }
    if dp.abs() > 0.1 {
        rationale_parts.push(format!("momentum:{}", dir_to_sym(dp)));
    }
    if dc.abs() > 0.1 {
        rationale_parts.push(format!("drive:{}", dir_to_sym(dc)));
    }

    // check for Δ cascades within timeframe
    for d in deltas {
        if d.trigger_code == vectors.code || d.target_code == vectors.code {
            let tau_h = parse_tau_to_hours(&d.tau);
            if tau_h <= timeframe_hours {
                rationale_parts.push(format!(
                    "Δ_cascade:{}_{}@{}h",
                    d.change.property, d.change.after, tau_h as u32
                ));
            }
        }
    }

    (dir_to_sym(predicted), rationale_parts.join(","))
}

// ═══════════════════════════════════════════════════════════════════
// Flag detection
// ═══════════════════════════════════════════════════════════════════

struct ConvergenceFlag {
    flag_type: String,
    expr: String,
}

fn detect_flags(
    all_vectors: &[SignalVectors],
    meta_ops: &[MetaOp],
    deltas: &[DeltaOp],
    _delta_logs: &[DeltaLog],
    _edges: &[Edge],
) -> Vec<ConvergenceFlag> {
    let mut flags = Vec::new();

    for v in all_vectors {
        // ⚡allo: σ̃ setpoint has drifted from developmental default (norm)
        if v.v_meta.direction.abs() > 0.1 {
            let baseline_str = if v.v_meta.direction > 1.0 { "++" }
                else if v.v_meta.direction > 0.0 { "+" }
                else if v.v_meta.direction < -1.0 { "--" }
                else { "-" };
            flags.push(ConvergenceFlag {
                flag_type: "allo".to_string(),
                expr: format!("σ̃{{{}@{}}}(baseline:=→{})", v.code, v.region, baseline_str),
            });
        }

        // ⚡resist: Δ0 intervention opposed by σ̃ setpoint
        let has_delta_up = deltas.iter().any(|d| {
            d.trigger_code == v.code && d.trigger_region == v.region
                && sym_direction(&d.trigger_state) > 0.0
        });
        let has_delta_down = deltas.iter().any(|d| {
            d.trigger_code == v.code && d.trigger_region == v.region
                && sym_direction(&d.trigger_state) < 0.0
        });

        if has_delta_up && v.v_meta.direction < -0.1 {
            flags.push(ConvergenceFlag {
                flag_type: "resist".to_string(),
                expr: format!("Δ0{{{}}}↑ opposed by σ̃{{{}@{}}}(baseline:{})",
                    v.code, v.code, v.region, v.v_meta.detail),
            });
        }
        if has_delta_down && v.v_meta.direction > 0.1 {
            flags.push(ConvergenceFlag {
                flag_type: "resist".to_string(),
                expr: format!("Δ0{{{}}}↓ opposed by σ̃{{{}@{}}}(baseline:{})",
                    v.code, v.code, v.region, v.v_meta.detail),
            });
        }

        // ⚡diverge: v_past trend contradicts v_meta direction
        if v.v_past.direction.abs() > 0.1 && v.v_meta.direction.abs() > 0.1 {
            let past_sign = v.v_past.direction > 0.0;
            let meta_sign = v.v_meta.direction > 0.0;
            if past_sign != meta_sign {
                flags.push(ConvergenceFlag {
                    flag_type: "diverge".to_string(),
                    expr: format!("trend(v_past:{}@{})={} ≠ σ̃({}@{})={}",
                        v.code, v.region, dir_to_sym(v.v_past.direction),
                        v.code, v.region, dir_to_sym(v.v_meta.direction)),
                });
            }
        }

        // ⚡unstable: all three vectors disagree
        let dp = v.v_past.direction;
        let dc = v.v_current.direction;
        let dm = v.v_meta.direction;
        let same = |a: f32, b: f32| (a > 0.0 && b > 0.0) || (a < 0.0 && b < 0.0);
        if !same(dp, dc) && !same(dc, dm) && !same(dp, dm)
            && dp.abs() > 0.1 && dc.abs() > 0.1 && dm.abs() > 0.1 {
            flags.push(ConvergenceFlag {
                flag_type: "unstable".to_string(),
                expr: format!("v_past≠v_current≠v_meta for {{{}@{}}}", v.code, v.region),
            });
        }
    }

    // ⚡lock: ⊲̃ has methylation/epigenetic lock
    for m in meta_ops {
        if m.rank_tag == "M2" {
            let prog = &m.target.program;
            if prog.contains("methylation") || prog.contains("epigenetic") || prog.contains("locked") {
                flags.push(ConvergenceFlag {
                    flag_type: "lock".to_string(),
                    expr: format!("⊲̃{{{}}}={}", m.target.code, prog),
                });
            }
        }
    }

    // ⚡cascade: Δn will trigger Δ(n+1) within prediction horizon
    let mut delta_by_rank: HashMap<u8, Vec<&DeltaOp>> = HashMap::new();
    for d in deltas {
        let rank = d.rank_tag.trim_start_matches('Δ').parse::<u8>().unwrap_or(0);
        delta_by_rank.entry(rank).or_default().push(d);
    }

    for rank in 0..3u8 {
        if let Some(lower) = delta_by_rank.get(&rank) {
            if let Some(upper) = delta_by_rank.get(&(rank + 1)) {
                for dl in lower {
                    for du in upper {
                        // check if lower's target is upper's trigger
                        if dl.target_code == du.trigger_code && dl.target_region == du.trigger_region {
                            let tau_remaining = parse_tau_to_hours(&du.tau);
                            flags.push(ConvergenceFlag {
                                flag_type: "cascade".to_string(),
                                expr: format!("Δ{}{{{}@{}}}→Δ{}{{{}@{}}} [τ_remaining:~{}h]",
                                    rank, dl.target_code, dl.target_region,
                                    rank + 1, du.target_code, du.target_region,
                                    tau_remaining as u32),
                            });
                        }
                    }
                }
            }
        }
    }

    flags
}

// ═══════════════════════════════════════════════════════════════════
// Main convergence reducer
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn compute_convergence(ctx: &ReducerContext, program_id: u64) -> Result<(), String> {
    let nodes: Vec<Node> = ctx.db.node().by_program().filter(program_id).collect();
    let edges: Vec<Edge> = ctx.db.edge().by_program().filter(program_id).collect();
    let deltas: Vec<DeltaOp> = ctx.db.delta_op().by_program().filter(program_id).collect();
    let delta_logs: Vec<DeltaLog> = ctx.db.delta_log().by_program().filter(program_id).collect();
    let meta_ops: Vec<MetaOp> = ctx.db.meta_op().by_program().filter(program_id).collect();

    // clear existing convergence data for this program
    let old_convs: Vec<u64> = ctx.db.conv().by_program().filter(program_id)
        .map(|c| c.id).collect();
    for cid in old_convs {
        if let Some(c) = ctx.db.conv().id().find(cid) {
            ctx.db.conv().id().delete(c.id);
        }
    }

    // identify major signal nodes (R0 nodes with non-trivial state)
    let signal_nodes: Vec<&Node> = nodes.iter()
        .filter(|n| n.rank_tag == "R0")
        .filter(|n| {
            n.state.as_ref().map_or(false, |s| !s.sym.is_empty() || s.val.is_some())
        })
        .collect();

    let mut all_vectors = Vec::new();

    // ─── Compute ∮ convergence state for each signal ───
    for node in &signal_nodes {
        let code = &node.code;
        let region = node.region.as_deref().unwrap_or("");

        let v_past = compute_v_past(code, region, &deltas, &delta_logs);
        let v_current = compute_v_current(code, region, &nodes);
        let v_meta = compute_v_meta(code, region, &meta_ops);

        let vectors = SignalVectors {
            code: code.clone(),
            region: region.to_string(),
            v_past: v_past.clone(),
            v_current: v_current.clone(),
            v_meta: v_meta.clone(),
        };

        let diagnosis = diagnose(&vectors);

        // store ∮ state
        ctx.db.conv().insert(Conv {
            id: 0, program_id, kind: "state".to_string(),
            signal_code: Some(code.clone()),
            signal_region: Some(region.to_string()),
            vectors: Some(vec![
                ConvVector { source: "v_past".to_string(), state: dir_to_sym(v_past.direction), detail: Some(v_past.detail.clone()) },
                ConvVector { source: "v_current".to_string(), state: dir_to_sym(v_current.direction), detail: Some(v_current.detail.clone()) },
                ConvVector { source: "v_meta".to_string(), state: dir_to_sym(v_meta.direction), detail: Some(v_meta.detail.clone()) },
            ]),
            diagnosis: Some(diagnosis.clone()),
            timeframe: None, predicted: None, rationale: None, confidence: None,
            flag_type: None, flag_expr: None,
            risk_name: None, risk_target: None, risk_distance: None,
            risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
            monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
        });

        all_vectors.push(vectors);
    }

    // ─── Compute ⊳ trajectory predictions ───
    let prediction_horizons = [4.0 * 168.0, 3.0 * 720.0, 6.0 * 720.0]; // 4wk, 3mo, 6mo in hours

    for vectors in &all_vectors {
        for &horizon_h in &prediction_horizons {
            let (predicted, rationale) = predict_trajectory(
                vectors, horizon_h, &deltas, &delta_logs,
            );

            let timeframe = if horizon_h < 168.0 * 2.0 {
                format!("{}d", (horizon_h / 24.0) as u32)
            } else if horizon_h < 720.0 * 2.0 {
                format!("{}wk", (horizon_h / 168.0) as u32)
            } else {
                format!("{}mo", (horizon_h / 720.0) as u32)
            };

            ctx.db.conv().insert(Conv {
                id: 0, program_id, kind: "predict".to_string(),
                signal_code: Some(vectors.code.clone()),
                signal_region: Some(vectors.region.clone()),
                vectors: None, diagnosis: None,
                timeframe: Some(timeframe),
                predicted: Some(predicted),
                rationale: Some(rationale), confidence: None,
                flag_type: None, flag_expr: None,
                risk_name: None, risk_target: None, risk_distance: None,
                risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
                monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
            });
        }
    }

    // ─── Detect ⚡ flags ───
    let flags = detect_flags(&all_vectors, &meta_ops, &deltas, &delta_logs, &edges);
    for f in &flags {
        ctx.db.conv().insert(Conv {
            id: 0, program_id, kind: "flag".to_string(),
            signal_code: None, signal_region: None,
            vectors: None, diagnosis: None,
            timeframe: None, predicted: None, rationale: None, confidence: None,
            flag_type: Some(f.flag_type.clone()),
            flag_expr: Some(f.expr.clone()),
            risk_name: None, risk_target: None, risk_distance: None,
            risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
            monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
        });
    }

    log::info!("CONVERGENCE|signals:{}|predictions:{}|flags:{}",
        all_vectors.len(), all_vectors.len() * prediction_horizons.len(), flags.len());

    Ok(())
}
