use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::base::tables::*;
use crate::plasticity::tables::*;
use crate::sim::tables::*;
use crate::parser::parse_tau_to_hours;
use std::collections::HashMap;

// ═══════════════════════════════════════════════════════════════════
// Constants
// ═══════════════════════════════════════════════════════════════════

const TICK_HOURS: f32 = 1.0;
const EPSILON: f32 = 1e-4;
const SATURATION_MIN: f32 = -1.0;
const SATURATION_MAX: f32 = 1.0;
const MAX_RING_PASSES: u32 = 3; // max ring propagation passes per tick
const DECAY: f32 = 0.5; // per-tick decay toward baseline (0=instant, 1=no decay)

// ═══════════════════════════════════════════════════════════════════
// Per-tick node state (in-memory, not persisted per-tick)
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
struct NodeSlot {
    node_id: u64,
    code: String,
    region: String,
    kind: String,
    value: f32,
    active: bool,
    is_root: bool,
}

// ═══════════════════════════════════════════════════════════════════
// Edge with resolved data for fast iteration
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
struct EdgeSlot {
    edge_id: u64,
    source_idx: usize,
    target_idx: usize,
    edge_type: String,
    coeff: f32,
    gain: f32,
    gate_node_idx: Option<usize>,
    gate_threshold: f32,
    ring_id: Option<String>,
}

// ═══════════════════════════════════════════════════════════════════
// Integration unit for barrier sync
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
struct IntegSlot {
    node_idx: usize,
    inputs: Vec<IntegInputSlot>,
    mode: String,
}

#[derive(Clone, Debug)]
struct IntegInputSlot {
    node_idx: usize,
    weight: f32,
    w_type: String, // exc | inh | mod
}

// ═══════════════════════════════════════════════════════════════════
// Conditional (tensor) slot
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
struct TensorSlot {
    tensor_id: u64,
    conditions: Vec<CondSlot>,
    logic: String, // AND | OR
    effect_node_idx: Option<usize>,
    effect_action: String,
    effect_value: Option<f32>,
}

#[derive(Clone, Debug)]
struct CondSlot {
    node_idx: Option<usize>,
    threshold: f32,
    negated: bool,
}

// ═══════════════════════════════════════════════════════════════════
// Delta (plasticity) slot
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
struct DeltaSlot {
    delta_id: u64,
    rank: u8, // 0-3
    trigger_idx: Option<usize>,
    trigger_threshold: f32,
    tau_hours: f32,
    target_idx: Option<usize>,
    change_prop: String,
    change_before: String,
    change_after: String,
}

// ═══════════════════════════════════════════════════════════════════
// Trace entry for evidence trail
// ═══════════════════════════════════════════════════════════════════

struct TraceEvent {
    tick: u32,
    node_idx: usize,
    event: String,
    value: f32,
    delta: f32,
    sources: Vec<u64>,
}

// ═══════════════════════════════════════════════════════════════════
// State symbol to numeric conversion
// ═══════════════════════════════════════════════════════════════════

fn sym_to_val(sym: &str) -> f32 {
    match sym {
        "++" => 0.8,
        "+" => 0.4,
        "=" => 0.0,
        "-" => -0.4,
        "--" => -0.8,
        "~" => 0.0,
        "X" => -1.0,
        "*" => 0.8,
        // legacy unicode
        "↑↑" => 0.8, "↑" => 0.4, "≈" => 0.0, "↓" => -0.4, "↓↓" => -0.8,
        "⊘" => -1.0, "●" => -1.0,
        _ => 0.0,
    }
}

fn threshold_from_sym(sym: &str) -> f32 {
    match sym {
        "++" => 0.6,
        "+" => 0.2,
        "=" => -0.2,
        "-" => -0.6,
        "--" => -1.0,
        // legacy unicode
        "↑↑" => 0.6, "↑" => 0.2, "≈" => -0.2, "↓" => -0.6, "↓↓" => -1.0,
        _ => 0.0,
    }
}

// ═══════════════════════════════════════════════════════════════════
// Build execution graph from DB tables
// ═══════════════════════════════════════════════════════════════════

fn build_node_slots(ctx: &ReducerContext, program_id: u64) -> (Vec<NodeSlot>, HashMap<String, usize>, HashMap<u64, usize>) {
    let nodes: Vec<Node> = ctx.db.node().by_program().filter(program_id).collect();

    let mut slots = Vec::with_capacity(nodes.len());
    let mut key_to_idx: HashMap<String, usize> = HashMap::new();
    let mut id_to_idx: HashMap<u64, usize> = HashMap::new();

    for node in &nodes {
        let idx = slots.len();
        let value = node.state.as_ref().map_or(0.0, |s| {
            s.val.unwrap_or_else(|| sym_to_val(&s.sym))
        });
        let key = format!("{}@{}", node.code, node.region.as_deref().unwrap_or(""));
        key_to_idx.insert(key.clone(), idx);
        id_to_idx.insert(node.id, idx);
        slots.push(NodeSlot {
            node_id: node.id,
            code: node.code.clone(),
            region: node.region.clone().unwrap_or_default(),
            kind: node.kind.clone(),
            value,
            active: true, // all nodes active by default; only block silences
            is_root: node.is_root,
        });
    }
    (slots, key_to_idx, id_to_idx)
}

fn build_edge_slots(
    ctx: &ReducerContext,
    program_id: u64,
    id_to_idx: &HashMap<u64, usize>,
    key_to_idx: &HashMap<String, usize>,
) -> Vec<EdgeSlot> {
    let edges: Vec<Edge> = ctx.db.edge().by_program().filter(program_id).collect();
    let mut slots = Vec::new();

    for edge in &edges {
        if edge.rank_tag != "R0" { continue; }
        let src = match id_to_idx.get(&edge.source_id) {
            Some(&i) => i,
            None => continue,
        };
        let tgt = match id_to_idx.get(&edge.target_id) {
            Some(&i) => i,
            None => continue,
        };

        let gain = edge.protocol.as_ref()
            .and_then(|p| p.gain)
            .unwrap_or(1.0);

        let gate_node_idx = edge.gate.as_ref().and_then(|g| {
            let key = format!("{}@{}", g.node_code, g.region);
            key_to_idx.get(&key).copied()
        });
        let gate_threshold = edge.gate.as_ref()
            .map(|g| threshold_from_sym(&g.threshold))
            .unwrap_or(f32::MIN);

        slots.push(EdgeSlot {
            edge_id: edge.id,
            source_idx: src,
            target_idx: tgt,
            edge_type: edge.edge_type.clone().unwrap_or_else(|| "→".to_string()),
            coeff: edge.coeff,
            gain,
            gate_node_idx,
            gate_threshold,
            ring_id: edge.ring_id.clone(),
        });
    }
    slots
}

fn build_integ_slots(
    ctx: &ReducerContext,
    program_id: u64,
    key_to_idx: &HashMap<String, usize>,
    id_to_idx: &HashMap<u64, usize>,
) -> Vec<IntegSlot> {
    let nodes: Vec<Node> = ctx.db.node().by_program().filter(program_id).collect();
    let mut slots = Vec::new();

    for node in &nodes {
        if node.rank_tag != "R1" { continue; }
        if let Some(ref integ) = node.integ {
            let node_idx = match id_to_idx.get(&node.id) {
                Some(&i) => i,
                None => continue,
            };
            let inputs: Vec<IntegInputSlot> = integ.inputs.iter().filter_map(|inp| {
                let key = format!("{}@{}", inp.code, inp.region);
                key_to_idx.get(&key).map(|&idx| IntegInputSlot {
                    node_idx: idx,
                    weight: inp.weight,
                    w_type: inp.w_type.clone(),
                })
            }).collect();

            slots.push(IntegSlot { node_idx, inputs, mode: integ.output.mode.clone() });
        }
    }
    slots
}

fn build_tensor_slots(
    ctx: &ReducerContext,
    program_id: u64,
    key_to_idx: &HashMap<String, usize>,
) -> Vec<TensorSlot> {
    let tensors: Vec<Tensor> = ctx.db.tensor().by_program().filter(program_id).collect();
    let mut slots = Vec::new();

    for t in &tensors {
        let conditions: Vec<CondSlot> = t.conditions.iter().map(|c| {
            let key = format!("{}@{}", c.code, c.region);
            CondSlot {
                node_idx: key_to_idx.get(&key).copied(),
                threshold: threshold_from_sym(&c.state),
                negated: c.negated,
            }
        }).collect();

        let eff_key = format!("{}@{}", t.effect.code, t.effect.region);
        let effect_node_idx = key_to_idx.get(&eff_key).copied();

        slots.push(TensorSlot {
            tensor_id: t.id,
            conditions,
            logic: t.logic.clone(),
            effect_node_idx,
            effect_action: t.effect.action.clone(),
            effect_value: t.effect.value,
        });
    }
    slots
}

fn build_delta_slots(
    ctx: &ReducerContext,
    program_id: u64,
    key_to_idx: &HashMap<String, usize>,
) -> Vec<DeltaSlot> {
    let deltas: Vec<DeltaOp> = ctx.db.delta_op().by_program().filter(program_id).collect();
    let mut slots = Vec::new();

    for d in &deltas {
        let rank = d.rank_tag.trim_start_matches('Δ').parse::<u8>().unwrap_or(0);
        let trig_key = format!("{}@{}", d.trigger_code, d.trigger_region);
        let tgt_key = format!("{}@{}", d.target_code, d.target_region);
        let tau_hours = parse_tau_to_hours(&d.tau);

        slots.push(DeltaSlot {
            delta_id: d.id,
            rank,
            trigger_idx: key_to_idx.get(&trig_key).copied(),
            trigger_threshold: threshold_from_sym(&d.trigger_state),
            tau_hours,
            target_idx: key_to_idx.get(&tgt_key).copied(),
            change_prop: d.change.property.clone(),
            change_before: d.change.before.clone(),
            change_after: d.change.after.clone(),
        });
    }
    slots
}

// ═══════════════════════════════════════════════════════════════════
// The Executor: tick-based propagation with 6 latches
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn simulate(
    ctx: &ReducerContext,
    program_id: u64,
    perturbations: Vec<Perturbation>,
    max_ticks: u32,
) -> Result<(), String> {
    // create SimRun record
    let run = ctx.db.sim_run().insert(SimRun {
        id: 0,
        program_id,
        max_ticks,
        perturbations: perturbations.clone(),
        status: "running".to_string(),
        final_tick: 0,
        started_at: ctx.timestamp,
    });
    let run_id = run.id;

    // build execution graph
    let (mut node_slots, key_to_idx, id_to_idx) = build_node_slots(ctx, program_id);
    let edge_slots = build_edge_slots(ctx, program_id, &id_to_idx, &key_to_idx);
    let integ_slots = build_integ_slots(ctx, program_id, &key_to_idx, &id_to_idx);
    let tensor_slots = build_tensor_slots(ctx, program_id, &key_to_idx);
    let delta_slots = build_delta_slots(ctx, program_id, &key_to_idx);

    // Save original DB values for final comparison (BUG 1 fix)
    let original_values: Vec<f32> = node_slots.iter().map(|s| s.value).collect();

    // apply initial perturbations (clamped to [-1, 1])
    for perturbation in &perturbations {
        let pert_key = format!("{}@{}", perturbation.target_code, perturbation.target_region);
        if let Some(&idx) = key_to_idx.get(&pert_key) {
            match perturbation.action.as_str() {
                "set" => {
                    node_slots[idx].value = perturbation.value.unwrap_or(1.0)
                        .clamp(SATURATION_MIN, SATURATION_MAX);
                    node_slots[idx].active = true;
                }
                "add" => {
                    node_slots[idx].value = (node_slots[idx].value + perturbation.value.unwrap_or(0.1))
                        .clamp(SATURATION_MIN, SATURATION_MAX);
                    node_slots[idx].active = true;
                }
                "block" => {
                    node_slots[idx].value = 0.0; // silent (BUG 3 fix: was -1.0)
                    node_slots[idx].active = false;
                }
                _ => {}
            }
        }
    }

    // ─── Double-buffered state arrays ───
    let n = node_slots.len();
    let mut state_current: Vec<f32> = node_slots.iter().map(|s| s.value).collect();
    let mut state_next: Vec<f32> = vec![0.0; n];

    // ─── τ accumulators (for delta rules) ───
    let mut delta_acc: HashMap<u64, f32> = HashMap::new();     // delta_id → accumulated

    // ─── Ring feedback queue (delayed by 1 tick) ───
    let mut ring_feedback: Vec<(usize, f32)> = Vec::new();


    // ─── Trace log ───
    let mut traces: Vec<TraceEvent> = Vec::new();

    let mut final_tick = 0u32;
    let mut status = "timeout";

    for tick in 1..=max_ticks {
        final_tick = tick;

        // ═══ READ PHASE (from state_current, frozen) ═══

        // Per-tick ring pass counter (constraint #3: max 3 passes per ring per tick)
        let mut ring_passes: HashMap<String, u32> = HashMap::new();

        // Apply ring feedback from previous tick
        for (idx, delta) in ring_feedback.drain(..) {
            state_current[idx] = (state_current[idx] + delta)
                .clamp(SATURATION_MIN, SATURATION_MAX);
        }

        // ═══ DECAY: non-root nodes decay toward baseline, roots hold ═══
        for i in 0..n {
            if node_slots[i].is_root {
                state_next[i] = state_current[i];
            } else {
                state_next[i] = state_current[i] * DECAY;
            }
        }

        // ═══ COMPUTE PHASE (all "parallel", reading from state_current) ═══

        // 1. Edge propagation (scaled by 1-DECAY so equilibrium = sum of inputs)
        for edge in &edge_slots {
            // BUG 3 fix: skip inactive (blocked) source nodes
            if !node_slots[edge.source_idx].active { continue; }
            let src_val = state_current[edge.source_idx];
            if src_val.abs() < EPSILON { continue; }

            // Gate check (conditional latch #6): evaluate against frozen state
            if let Some(gate_idx) = edge.gate_node_idx {
                if state_current[gate_idx] < edge.gate_threshold {
                    continue; // gate not open
                }
            }

            // Compute propagated value, scaled by (1-DECAY) for equilibrium = inputs
            let raw = src_val * edge.coeff * edge.gain;
            let propagated = raw * (1.0 - DECAY);

            // Check if this is a ring edge
            if let Some(ref ring_id) = edge.ring_id {
                // Ring damping latch (#4): feedback arrives next tick
                // Constraint #3: max passes per tick per ring
                let passes = ring_passes.entry(ring_id.clone()).or_insert(0);
                if *passes >= MAX_RING_PASSES {
                    continue; // ring has reached max passes this tick
                }
                *passes += 1;
                let damped = propagated * 0.1;
                ring_feedback.push((edge.target_idx, damped));
            } else {
                // Normal propagation: accumulate into state_next
                state_next[edge.target_idx] += propagated;
            }

            traces.push(TraceEvent {
                tick, node_idx: edge.target_idx,
                event: "propagate".to_string(),
                value: propagated, delta: propagated,
                sources: vec![edge.edge_id],
            });
        }

        // 2. Integration barriers (latch #3)
        for integ in &integ_slots {
            // Check if ALL inputs have resolved (value is non-zero in current state)
            let all_resolved = integ.inputs.iter().all(|inp| {
                state_current[inp.node_idx].abs() > EPSILON
            });

            if !all_resolved { continue; } // barrier not met

            // Compute integration sum
            let mut sum = 0.0f32;
            let mut mod_factor = 1.0f32;
            for inp in &integ.inputs {
                let v = state_current[inp.node_idx];
                match inp.w_type.as_str() {
                    "mod" => mod_factor *= v.abs().max(0.1) * inp.weight.signum(),
                    _ => sum += v * inp.weight,
                }
            }
            let result = sum * mod_factor;

            // Apply threshold based on mode
            let fires = match integ.mode.as_str() {
                "thr" => result.abs() > 0.5,
                "rate" => true, // always fires proportionally
                "burst" => result > 1.0,
                "tonic" => true, // continuous
                _ => result.abs() > EPSILON,
            };

            if fires {
                state_next[integ.node_idx] = result.clamp(SATURATION_MIN, SATURATION_MAX);
                traces.push(TraceEvent {
                    tick, node_idx: integ.node_idx,
                    event: "integrate".to_string(),
                    value: result, delta: result - state_current[integ.node_idx],
                    sources: integ.inputs.iter().map(|_| 0u64).collect(),
                });
            }
        }

        // 3. Conditional evaluation (latch #6: evaluate against frozen state_current)
        for tensor in &tensor_slots {
            let met = evaluate_conditions(&tensor.conditions, &tensor.logic, &state_current);
            if !met { continue; }

            if let Some(eff_idx) = tensor.effect_node_idx {
                match tensor.effect_action.as_str() {
                    "pass" => {
                        // no modification, signal passes through
                    }
                    "block" => {
                        state_next[eff_idx] = 0.0;
                        traces.push(TraceEvent {
                            tick, node_idx: eff_idx,
                            event: "conditional_block".to_string(),
                            value: 0.0, delta: -state_current[eff_idx],
                            sources: vec![tensor.tensor_id],
                        });
                    }
                    "amplify" => {
                        let factor = tensor.effect_value.unwrap_or(1.5);
                        state_next[eff_idx] = (state_current[eff_idx] * factor)
                            .clamp(SATURATION_MIN, SATURATION_MAX);
                        traces.push(TraceEvent {
                            tick, node_idx: eff_idx,
                            event: "conditional_amplify".to_string(),
                            value: state_next[eff_idx],
                            delta: state_next[eff_idx] - state_current[eff_idx],
                            sources: vec![tensor.tensor_id],
                        });
                    }
                    "switch" => {
                        // switch target — change routing
                    }
                    _ => {}
                }
            }
        }

        // ═══ CONSTRAIN PHASE (physics rules, after all COMPUTE) ═══

        // Constraint #2: Receptor saturation — R: nodes only, bidirectional toward ±1.0
        // Positive signal approaching +1.0: effective = signal × (1.0 - occupancy)
        // Negative signal approaching -1.0: effective = signal × (1.0 + occupancy)
        for i in 0..n {
            if node_slots[i].kind != "R" { continue; }
            let added = state_next[i] - state_current[i];
            if added.abs() < EPSILON { continue; }
            if added > 0.0 {
                // Approaching +1.0: headroom shrinks as value rises
                let headroom = (1.0 - state_current[i]).max(0.0);
                state_next[i] = state_current[i] + added * headroom;
            } else {
                // Approaching -1.0: headroom shrinks as value drops
                let headroom = (1.0 + state_current[i]).max(0.0);
                state_next[i] = state_current[i] + added * headroom;
            }
        }

        // ═══ WRITE PHASE (clamp and commit) ═══

        for i in 0..n {
            state_next[i] = state_next[i].clamp(SATURATION_MIN, SATURATION_MAX);
        }

        // ═══ Δ PHASE (between ticks, ranked serial latch #5) ═══

        for rank in 0..=3u8 {
            let mut _fired_any = false;
            for ds in &delta_slots {
                if ds.rank != rank { continue; }

                // Check trigger condition against state_next
                if let Some(trig_idx) = ds.trigger_idx {
                    let trig_val = state_next[trig_idx];
                    if trig_val >= ds.trigger_threshold {
                        // τ-gated firing (latch #2)
                        let acc = delta_acc.entry(ds.delta_id).or_insert(0.0);
                        *acc += TICK_HOURS;
                        if *acc >= ds.tau_hours {
                            // FIRE
                            if let Some(tgt_idx) = ds.target_idx {
                                // Apply plasticity effect
                                let delta = apply_plasticity_effect(
                                    state_next[tgt_idx],
                                    &ds.change_prop,
                                    &ds.change_after,
                                );
                                state_next[tgt_idx] = (state_next[tgt_idx] + delta)
                                    .clamp(SATURATION_MIN, SATURATION_MAX);
                                traces.push(TraceEvent {
                                    tick, node_idx: tgt_idx,
                                    event: format!("delta_{}_fire", ds.rank),
                                    value: state_next[tgt_idx], delta,
                                    sources: vec![ds.delta_id],
                                });
                                _fired_any = true;
                            }
                            // Log the delta firing
                            ctx.db.delta_log().insert(DeltaLog {
                                id: 0, program_id,
                                delta_op_id: ds.delta_id,
                                fired_at_tick: tick,
                                fired_at: ctx.timestamp,
                            });
                            *acc = 0.0; // reset accumulator
                        }
                    } else {
                        // Trigger condition not met — reset accumulator
                        delta_acc.insert(ds.delta_id, 0.0);
                    }
                }
            }
        }

        // ═══ SWAP (state_current = state_next) ═══

        let mut any_change = false;
        for i in 0..n {
            if (state_next[i] - state_current[i]).abs() > EPSILON {
                any_change = true;
            }
            state_current[i] = state_next[i];
        }

        // Check steady state
        if !any_change && ring_feedback.is_empty() {
            status = "steady_state";
            break;
        }
    }

    // ─── Write trace to SimTick table ───
    for t in &traces {
        ctx.db.sim_tick().insert(SimTick {
            id: 0,
            sim_run_id: run_id,
            tick: t.tick,
            node_id: node_slots[t.node_idx].node_id,
            value: t.value,
            delta: t.delta,
            event: t.event.clone(),
            sources: t.sources.clone(),
        });
    }

    // ─── Write τ accumulators for resumability ───
    for (delta_id, acc) in &delta_acc {
        if *acc > 0.0 {
            ctx.db.tau_acc().insert(TauAccumulator {
                id: 0,
                sim_run_id: run_id,
                trigger_id: *delta_id,
                trigger_kind: "delta".to_string(),
                accumulated: *acc,
                tau_target: delta_slots.iter()
                    .find(|d| d.delta_id == *delta_id)
                    .map(|d| d.tau_hours)
                    .unwrap_or(0.0),
            });
        }
    }

    // ─── Update node states in DB with final values ───
    // Compare against original DB values (before perturbation) — BUG 1 fix
    for (i, slot) in node_slots.iter().enumerate() {
        if (state_current[i] - original_values[i]).abs() > EPSILON {
            if let Some(mut node) = ctx.db.node().id().find(slot.node_id) {
                let sym = val_to_sym(state_current[i]);
                node.state = Some(NodeState {
                    sym,
                    val: Some(state_current[i]),
                    delta_sign: None,
                    delta_val: None,
                });
                ctx.db.node().id().update(node);
            }
        }
    }

    // ─── Update program tick ───
    if let Some(mut prog) = ctx.db.program().id().find(program_id) {
        prog.tick = final_tick;
        ctx.db.program().id().update(prog);
    }

    // ─── Finalize SimRun ───
    let mut run = ctx.db.sim_run().id().find(run_id).unwrap();
    run.status = status.to_string();
    run.final_tick = final_tick;
    ctx.db.sim_run().id().update(run);

    log::info!("SIMULATE|run:{}|ticks:{}|status:{}|traces:{}",
        run_id, final_tick, status, traces.len());

    Ok(())
}

// ═══════════════════════════════════════════════════════════════════
// Helper: evaluate conditional conditions
// ═══════════════════════════════════════════════════════════════════

fn evaluate_conditions(conditions: &[CondSlot], logic: &str, state: &[f32]) -> bool {
    if conditions.is_empty() { return false; }

    let results: Vec<bool> = conditions.iter().map(|c| {
        let met = match c.node_idx {
            Some(idx) => state[idx] >= c.threshold,
            None => false,
        };
        if c.negated { !met } else { met }
    }).collect();

    match logic {
        "AND" => results.iter().all(|&r| r),
        "OR" => results.iter().any(|&r| r),
        _ => results.iter().all(|&r| r),
    }
}

// ═══════════════════════════════════════════════════════════════════
// Helper: apply plasticity effect
// ═══════════════════════════════════════════════════════════════════

fn apply_plasticity_effect(current: f32, prop: &str, after: &str) -> f32 {
    match prop {
        "release" => match after {
            "depleted" => -current.abs() * 0.5,
            "enhanced" => current.abs() * 0.3,
            _ => 0.0,
        },
        "baseline" => match after {
            // State arrow values (new format)
            "++" => 1.0,
            "+" => 0.5,
            "=" => 0.0,
            "~" => 0.0,
            "-" => -0.5,
            "--" => -1.0,
            // Legacy word values
            "low" | "lower" => -0.5,
            "high" => 0.5,
            "rising" => 0.3,
            "elevated" => 0.5,
            "reduced" => -0.3,
            _ => 0.0,
        },
        "gain" => {
            // gain changes: ×before→×after, parse the "after" as a multiplier
            if let Some(stripped) = after.strip_prefix('×') {
                stripped.parse::<f32>().unwrap_or(1.0) - 1.0
            } else {
                after.parse::<f32>().unwrap_or(1.0) - 1.0
            }
        }
        "spines" => match after {
            "increased" => 0.2,
            "reduced" | "pruned" => -0.3,
            _ => 0.0,
        },
        "dendrite" => match after {
            "retracted" => -0.3,
            "expanded" => 0.2,
            _ => 0.0,
        },
        "gate" => match after {
            "desens" | "closed" => -0.5,
            "open" => 0.5,
            _ => 0.0,
        },
        "st" => match after {
            "des" | "desens" => -0.3,
            "supersens" => 0.5,
            "intern" => -0.5,
            _ => 0.0,
        },
        "dens" => match after {
            "up" => 0.3,
            "down" => -0.3,
            _ => 0.0,
        },
        "activity" => match after {
            "up" => 0.3,
            "down" => -0.3,
            _ => 0.0,
        },
        "state" => match after {
            "activated" | "reactive" => 0.5,
            "surveilling" | "resting" => -0.3,
            _ => 0.0,
        },
        "volume" => match after {
            "reduced" => -0.5,
            "hypertrophied" => 0.3,
            _ => 0.0,
        },
        "neurogenesis" => match after {
            "increasing" | "increased" => 0.3,
            "decreased" => -0.3,
            _ => 0.0,
        },
        _ => 0.0,
    }
}

// ═══════════════════════════════════════════════════════════════════
// Helper: numeric value back to symbol
// ═══════════════════════════════════════════════════════════════════

fn val_to_sym(v: f32) -> String {
    if v >= 0.6 { "++".to_string() }
    else if v >= 0.2 { "+".to_string() }
    else if v >= -0.2 { "=".to_string() }
    else if v >= -0.6 { "-".to_string() }
    else { "--".to_string() }
}

// ═══════════════════════════════════════════════════════════════════
// Evidence trace retrieval
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn get_evidence_trace(ctx: &ReducerContext, sim_run_id: u64) -> Result<(), String> {
    let ticks: Vec<SimTick> = ctx.db.sim_tick().by_run().filter(sim_run_id).collect();

    for t in &ticks {
        log::info!("TRACE|tick:{}|node:{}|event:{}|val:{:.3}|delta:{:.3}|src:{:?}",
            t.tick, t.node_id, t.event, t.value, t.delta, t.sources);
    }

    log::info!("TRACE|total:{}", ticks.len());
    Ok(())
}
