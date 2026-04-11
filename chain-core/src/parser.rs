use spacetimedb::{reducer, ReducerContext, Table};
use crate::types::*;
use crate::db::base::tables::*;
use crate::db::plasticity::tables::*;
use crate::db::meta::tables::*;
use crate::db::convergence::tables::*;
use std::collections::HashMap;

pub use crate::parser_core::*;

// ═══════════════════════════════════════════════════════════════════
// Ingest reducer: parse + validate + insert
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn ingest_bnf(
    ctx: &ReducerContext,
    program_id: u64,
    pipeline: String,
    bnf_text: String,
) -> Result<(), String> {
    // store raw text
    let mut prog = ctx.db.program().id().find(program_id)
        .ok_or("Program not found")?;
    match pipeline.as_str() {
        "base" => prog.raw_base = Some(bnf_text.clone()),
        "plasticity" => prog.raw_plasticity = Some(bnf_text.clone()),
        "meta" => prog.raw_meta = Some(bnf_text.clone()),
        "convergence" => prog.raw_convergence = Some(bnf_text.clone()),
        _ => return Err(format!("Unknown pipeline: {}", pipeline)),
    }
    ctx.db.program().id().update(prog);

    match pipeline.as_str() {
        "base" => ingest_base(ctx, program_id, &bnf_text),
        "plasticity" => ingest_plasticity(ctx, program_id, &bnf_text),
        "meta" => ingest_meta(ctx, program_id, &bnf_text),
        "convergence" => ingest_convergence(ctx, program_id, &bnf_text),
        _ => unreachable!(),
    }
}

fn ingest_base(ctx: &ReducerContext, program_id: u64, text: &str) -> Result<(), String> {
    let vocab = biochain_vocab();
    let parsed = parse_base(text, &vocab)?;

    // update program domains/phase
    let mut prog = ctx.db.program().id().find(program_id).unwrap();
    prog.domains = parsed.domains;
    prog.phase = parsed.phase;
    ctx.db.program().id().update(prog);

    // symbol table: kind:code@region -> node_id (primary, type-aware)
    // secondary index: code@region -> first node_id (for seeds/protocols that lack kind)
    let mut sym: HashMap<String, u64> = HashMap::new();
    let mut by_code: HashMap<String, u64> = HashMap::new();

    // insert chain nodes and edges
    let mut chain_idx = 0u32;
    for chain in &parsed.chains {
        let chain_name = format!("chain_{}", chain_idx);
        chain_idx += 1;
        insert_chain_elements(ctx, program_id, chain, &chain_name, &mut sym, &mut by_code, 0)?;
    }

    // insert seeds as root markers (lookup by code@region since Δ has no type prefix)
    for seed in &parsed.seeds {
        let key = format!("{}@{}", seed.code, seed.region);
        if let Some(&nid) = by_code.get(&key) {
            let mut node = ctx.db.node().id().find(nid).unwrap();
            node.is_root = true;
            if node.state.is_none() {
                node.state = Some(NodeState {
                    sym: String::new(), val: None,
                    delta_sign: Some(seed.sign.clone()),
                    delta_val: Some(seed.val),
                });
            } else {
                let st = node.state.as_mut().unwrap();
                st.delta_sign = Some(seed.sign.clone());
                st.delta_val = Some(seed.val);
            }
            ctx.db.node().id().update(node);
        }
    }

    // insert integrations with edges from inputs → ∫ node → output
    for integ in &parsed.integrations {
        let integ_region = integ.unit.region.as_deref().unwrap_or("");
        let typed_key = format!("{}:{}@{}", integ.unit.kind, integ.unit.code, integ_region);
        let bare_key = format!("{}@{}", integ.unit.code, integ_region);

        // reuse existing node or insert new one
        let integ_node_id = if let Some(&existing) = sym.get(&typed_key) {
            existing
        } else {
            let node = ctx.db.node().insert(Node {
                id: 0, program_id,
                code: integ.unit.code.clone(),
                kind: integ.unit.kind.clone(),
                region: integ.unit.region.clone(),
                rank_tag: "R1".to_string(),
                state: None,
                integ: Some(Integration {
                    inputs: integ.inputs.iter().map(|i| IntegInput {
                        code: i.code.clone(), region: i.region.clone(),
                        weight: i.val.unwrap_or(1.0) * if i.sign == "-" { -1.0 } else { 1.0 },
                        w_type: match i.sign.as_str() {
                            "+" => "exc", "-" => "inh", "×" => "mod", _ => "exc"
                        }.to_string(),
                    }).collect(),
                    output: IntegOutput {
                        code: integ.output_code.clone(),
                        region: integ.output_region.clone(),
                        mode: integ.mode.clone(),
                        threshold: None,
                    },
                }),
                props: Vec::new(), is_root: false, terminal: None,
            });
            sym.insert(typed_key, node.id);
            by_code.entry(bare_key).or_insert(node.id);
            node.id
        };

        // wire edges: each input → ∫ node
        for inp in &integ.inputs {
            let inp_key = format!("{}@{}", inp.code, inp.region);
            if let Some(&src_id) = by_code.get(&inp_key) {
                let edge_type = match inp.sign.as_str() {
                    "-" => "⊣",
                    "×" => "~>",
                    _ => "→",
                };
                ctx.db.edge().insert(Edge {
                    id: 0, program_id,
                    source_id: src_id, target_id: integ_node_id,
                    rank_tag: "R1".to_string(),
                    edge_type: Some(edge_type.to_string()), coeff: 1.0,
                    gate: None, protocol: None, proto_label: None,
                    chain: Some("integ".to_string()),
                    chain_pos: None, ring_id: None,
                });
            }
        }

        // wire edge: ∫ node → output
        let out_key = format!("{}@{}", integ.output_code, integ.output_region);
        if let Some(&tgt_id) = by_code.get(&out_key) {
            ctx.db.edge().insert(Edge {
                id: 0, program_id,
                source_id: integ_node_id, target_id: tgt_id,
                rank_tag: "R1".to_string(),
                edge_type: Some("→".to_string()), coeff: 1.0,
                gate: None, protocol: None, proto_label: None,
                chain: Some("integ".to_string()),
                chain_pos: None, ring_id: None,
            });
        }
    }

    // insert protocols as R2 edges (lookup by code@region since protocols lack kind)
    for proto in &parsed.protocols {
        let src_key = format!("{}@{}", proto.source_code, proto.source_region.as_deref().unwrap_or(""));
        let src_id = by_code.get(&src_key).copied().unwrap_or(0);

        ctx.db.edge().insert(Edge {
            id: 0, program_id,
            source_id: src_id, target_id: 0, // target is the edge itself
            rank_tag: "R2".to_string(),
            edge_type: None, coeff: 1.0,
            gate: proto.gate_code.as_ref().map(|gc| GateSpec {
                node_code: gc.clone(),
                region: proto.gate_region.clone().unwrap_or_default(),
                threshold: proto.gate_threshold.clone().unwrap_or_default(),
            }),
            protocol: Some(ProtocolSpec {
                gain: proto.gain,
                polarity: proto.polarity.clone(),
                tau_class: proto.tau_class.clone(),
                tau_value: proto.tau_value.clone(),
                gate: proto.gate_threshold.clone(),
                coupling: proto.coupling.clone(),
                release_pr: None,
            }),
            proto_label: Some(proto.edge_label.clone()),
            chain: None, chain_pos: None, ring_id: None,
        });
    }

    // insert conditionals
    for cond in &parsed.conditionals {
        ctx.db.tensor().insert(Tensor {
            id: 0, program_id,
            conditions: cond.conditions.iter().map(|c| TensorCond {
                code: c.code.clone(), region: c.region.clone(),
                state: c.threshold.clone(), negated: c.negated,
            }).collect(),
            logic: if cond.logic == "∧" { "AND" } else { "OR" }.to_string(),
            effect: TensorEffect {
                code: cond.effect_code.clone(),
                region: cond.effect_region.clone(),
                action: cond.effect_action.clone(),
                value: cond.effect_value,
                switch_to: cond.effect_switch.clone(),
            },
            label: None,
        });
    }

    // insert composites
    for comp in &parsed.composites {
        let refs_str: Vec<String> = comp.refs.iter()
            .map(|(c, r)| format!("{}@{}", c, r)).collect();
        ctx.db.diag().insert(Diag {
            id: 0, program_id,
            kind: "composite".to_string(),
            name: Some(comp.name.clone()),
            expr: refs_str.join("+"),
            detail: Vec::new(),
        });
    }

    // insert dysregs
    for dys in &parsed.dysregs {
        let chain_str = format!("{:?}", dys.elements); // simplified
        ctx.db.diag().insert(Diag {
            id: 0, program_id,
            kind: "dysreg".to_string(),
            name: Some(dys.dtype.clone()),
            expr: chain_str,
            detail: Vec::new(),
        });
    }

    log::info!("INGEST_BASE|nodes:{}|edges:{}",
        ctx.db.node().by_program().filter(program_id).count(),
        ctx.db.edge().by_program().filter(program_id).count());

    Ok(())
}

fn insert_chain_elements(
    ctx: &ReducerContext,
    program_id: u64,
    elements: &[ChainElement],
    chain_name: &str,
    sym: &mut HashMap<String, u64>,
    by_code: &mut HashMap<String, u64>,
    mut pos: u32,
) -> Result<u32, String> {
    let mut prev_node_id: Option<u64> = None;
    let mut pending_edge: Option<String> = None;

    for el in elements {
        match el {
            ChainElement::Node(pn) => {
                let region_str = pn.region.as_deref().unwrap_or("");
                let key = format!("{}:{}@{}", pn.kind, pn.code, region_str);
                let bare_key = format!("{}@{}", pn.code, region_str);
                let node_id = if let Some(&existing) = sym.get(&key) {
                    // update state if this declaration has new info
                    if pn.state.is_some() {
                        let mut node = ctx.db.node().id().find(existing).unwrap();
                        node.state = pn.state.as_ref().map(|s| NodeState {
                            sym: s.sym.clone(), val: s.val,
                            delta_sign: s.delta_sign.clone(), delta_val: s.delta_val,
                        });
                        ctx.db.node().id().update(node);
                    }
                    existing
                } else {
                    let node = ctx.db.node().insert(Node {
                        id: 0, program_id,
                        code: pn.code.clone(),
                        kind: pn.kind.clone(),
                        region: pn.region.clone(),
                        rank_tag: "R0".to_string(),
                        state: pn.state.as_ref().map(|s| NodeState {
                            sym: s.sym.clone(), val: s.val,
                            delta_sign: s.delta_sign.clone(), delta_val: s.delta_val,
                        }),
                        integ: None,
                        props: pn.props.iter().map(|(k, v)| Kv { k: k.clone(), v: v.clone() }).collect(),
                        is_root: pn.is_root,
                        terminal: pn.terminal.clone(),
                    });
                    sym.insert(key, node.id);
                    by_code.entry(bare_key).or_insert(node.id);
                    node.id
                };

                // create edge from previous node if pending
                if let (Some(src), Some(etype)) = (prev_node_id, pending_edge.take()) {
                    let coeff = match etype.as_str() {
                        "→" => 1.0,
                        "⊣" => -1.0,
                        "~>" => 1.0,
                        "=>" => 1.0,
                        "|>" => 1.0,
                        _ => 1.0,
                    };
                    ctx.db.edge().insert(Edge {
                        id: 0, program_id,
                        source_id: src, target_id: node_id,
                        rank_tag: "R0".to_string(),
                        edge_type: Some(etype), coeff,
                        gate: None, protocol: None, proto_label: None,
                        chain: Some(chain_name.to_string()),
                        chain_pos: Some(pos),
                        ring_id: None,
                    });
                    pos += 1;
                }

                prev_node_id = Some(node_id);
            }
            ChainElement::Edge(etype) => {
                pending_edge = Some(etype.clone());
            }
            ChainElement::Branch(branches) => {
                let branch_src = prev_node_id;
                for (bi, branch) in branches.iter().enumerate() {
                    let sub_chain = format!("{}_b{}", chain_name, bi);
                    // reset prev_node to branch source for each branch
                    prev_node_id = branch_src;
                    pending_edge = None;
                    pos = insert_chain_elements(ctx, program_id, branch, &sub_chain, sym, by_code, pos)?;
                }
                // after branches, prev_node is undefined (branches may diverge)
                prev_node_id = branch_src;
            }
            ChainElement::Gate(_code, _region) => {
                // gate doesn't create a node, it's a condition on the next edge
            }
            ChainElement::Terminal(term_type) => {
                // terminal — mark last node with terminal type and end chain
                if let Some(nid) = prev_node_id {
                    if let Some(mut node) = ctx.db.node().id().find(nid) {
                        node.terminal = term_type.clone();
                        ctx.db.node().id().update(node);
                    }
                }
                prev_node_id = None;
            }
        }
    }

    Ok(pos)
}

fn ingest_plasticity(ctx: &ReducerContext, program_id: u64, text: &str) -> Result<(), String> {
    let vocab = biochain_vocab();
    let deltas = parse_plasticity(text, &vocab)?;

    // validate triggers against existing nodes
    for d in &deltas {
        let key = format!("{}@{}", d.trigger_code, d.trigger_region);
        let found = ctx.db.node().by_program().filter(program_id)
            .any(|n| {
                let nkey = format!("{}@{}", n.code, n.region.as_deref().unwrap_or(""));
                nkey == key
            });
        if !found {
            log::warn!("WARN: Δ trigger {}@{} not found in BASE", d.trigger_code, d.trigger_region);
        }
    }

    for d in deltas {
        ctx.db.delta_op().insert(DeltaOp {
            id: 0, program_id,
            rank_tag: format!("Δ{}", d.rank),
            trigger_code: d.trigger_code,
            trigger_region: d.trigger_region,
            trigger_state: d.trigger_state,
            target_code: d.target_code,
            target_region: d.target_region,
            change: PropChange {
                property: d.change_prop,
                before: d.change_before,
                after: d.change_after,
            },
            tau: d.tau,
            depends: d.depends,
            status: d.status,
            cascade_name: d.cascade_name,
            tensor_expr: d.tensor_expr,
        });
    }

    log::info!("INGEST_PLASTICITY|deltas:{}",
        ctx.db.delta_op().by_program().filter(program_id).count());
    Ok(())
}

fn ingest_meta(ctx: &ReducerContext, program_id: u64, text: &str) -> Result<(), String> {
    let vocab = biochain_vocab();
    let entries = parse_meta(text, &vocab)?;

    for e in entries {
        ctx.db.meta_op().insert(MetaOp {
            id: 0, program_id,
            rank_tag: e.rank,
            window: MetaWindow { kind: e.window_kind, value: e.window_value },
            target: MetaTarget {
                code: e.target_code,
                region: e.target_region,
                property: e.target_property,
                program: e.target_program,
                reversible: e.reversible,
                unlocks_with: e.unlocks_with,
                pull: e.pull,
            },
        });
    }

    log::info!("INGEST_META|entries:{}",
        ctx.db.meta_op().by_program().filter(program_id).count());
    Ok(())
}

fn ingest_convergence(ctx: &ReducerContext, program_id: u64, text: &str) -> Result<(), String> {
    let vocab = biochain_vocab();
    let entries = parse_convergence(text, &vocab)?;

    for e in entries {
        match e {
            ParsedConvEntry::State { signal_code, signal_region, vectors, diagnosis } => {
                ctx.db.conv().insert(Conv {
                    id: 0, program_id, kind: "state".to_string(),
                    signal_code: Some(signal_code), signal_region: Some(signal_region),
                    vectors: Some(vectors.into_iter().map(|(src, st, det)| ConvVector {
                        source: src, state: st, detail: if det.is_empty() { None } else { Some(det) },
                    }).collect()),
                    diagnosis: Some(diagnosis),
                    timeframe: None, predicted: None, rationale: None, confidence: None,
                    flag_type: None, flag_expr: None,
                    risk_name: None, risk_target: None, risk_distance: None,
                    risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
                    monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
                });
            }
            ParsedConvEntry::Trajectory { signal_code, signal_region, timeframe, predicted, rationale, confidence } => {
                ctx.db.conv().insert(Conv {
                    id: 0, program_id, kind: "predict".to_string(),
                    signal_code: Some(signal_code), signal_region: Some(signal_region),
                    vectors: None, diagnosis: None,
                    timeframe: Some(timeframe), predicted: Some(predicted),
                    rationale: Some(rationale), confidence,
                    flag_type: None, flag_expr: None,
                    risk_name: None, risk_target: None, risk_distance: None,
                    risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
                    monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
                });
            }
            ParsedConvEntry::Risk { risk_name, risk_target, risk_distance, risk_window,
                                    risk_reversible_before, risk_reversible_after } => {
                ctx.db.conv().insert(Conv {
                    id: 0, program_id, kind: "risk".to_string(),
                    signal_code: None, signal_region: None,
                    vectors: None, diagnosis: None,
                    timeframe: None, predicted: None, rationale: None, confidence: None,
                    flag_type: None, flag_expr: None,
                    risk_name: Some(risk_name), risk_target, risk_distance,
                    risk_window, risk_reversible_before, risk_reversible_after,
                    monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
                });
            }
            ParsedConvEntry::Monitor { measurement, flag_ref, note } => {
                ctx.db.conv().insert(Conv {
                    id: 0, program_id, kind: "monitor".to_string(),
                    signal_code: None, signal_region: None,
                    vectors: None, diagnosis: None,
                    timeframe: None, predicted: None, rationale: None, confidence: None,
                    flag_type: None, flag_expr: None,
                    risk_name: None, risk_target: None, risk_distance: None,
                    risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
                    monitor_measurement: Some(measurement), monitor_flag_ref: flag_ref, monitor_note: note,
                });
            }
            ParsedConvEntry::Flag { flag_type, expr } => {
                ctx.db.conv().insert(Conv {
                    id: 0, program_id, kind: "flag".to_string(),
                    signal_code: None, signal_region: None,
                    vectors: None, diagnosis: None,
                    timeframe: None, predicted: None, rationale: None, confidence: None,
                    flag_type: Some(flag_type), flag_expr: Some(expr),
                    risk_name: None, risk_target: None, risk_distance: None,
                    risk_window: None, risk_reversible_before: None, risk_reversible_after: None,
                    monitor_measurement: None, monitor_flag_ref: None, monitor_note: None,
                });
            }
        }
    }

    log::info!("INGEST_CONVERGENCE|entries:{}",
        ctx.db.conv().by_program().filter(program_id).count());
    Ok(())
}

// ═══════════════════════════════════════════════════════════════════
// Lint reducer: parse + validate raw BNF text without DB writes
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn lint_bnf(
    _ctx: &ReducerContext,
    pipeline: String,
    bnf_text: String,
) -> Result<(), String> {
    let vocab = biochain_vocab();
    match pipeline.as_str() {
        "base" => {
            let result = lint_base(&bnf_text, &vocab);
            log::info!(
                "LINT_BASE|valid:{}|nodes:{}|edges:{}|chains:{}|issues:{}",
                result.valid, result.node_count, result.edge_count,
                result.chain_count, result.issues.len()
            );
            for issue in &result.issues {
                let level = match issue.level {
                    LintLevel::Error => "ERROR",
                    LintLevel::Warn => "WARN",
                };
                log::info!("LINT|{}|{}", level, issue.message);
            }
            if result.valid { Ok(()) } else {
                Err(format!("Lint failed with {} issues", result.issues.len()))
            }
        }
        "plasticity" => {
            let deltas = parse_plasticity(&bnf_text, &vocab)?;
            log::info!("LINT_PLASTICITY|ok|deltas:{}", deltas.len());
            Ok(())
        }
        "meta" => {
            let entries = parse_meta(&bnf_text, &vocab)?;
            log::info!("LINT_META|ok|entries:{}", entries.len());
            Ok(())
        }
        "convergence" => {
            let entries = parse_convergence(&bnf_text, &vocab)?;
            log::info!("LINT_CONVERGENCE|ok|entries:{}", entries.len());
            Ok(())
        }
        _ => Err(format!("Unknown pipeline: {}", pipeline)),
    }
}
