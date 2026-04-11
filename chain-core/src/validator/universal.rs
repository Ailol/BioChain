//! Pass 1 — Universal closure invariants.
//! These checks fire identically against any Chain domain. They verify
//! the diamond's structural contract, not any domain's biology or logic.
//! Zero domain-specific string literals appear in this file.

use super::common::*;

/// Run all universal structural checks against a program snapshot.
pub fn check_universal(snap: &ProgramSnapshot) -> Vec<ValidationError> {
    let mut errors = Vec::new();

    check_declaration_before_use(snap, &mut errors);
    check_ring_closure(snap, &mut errors);
    check_protocol_targets(snap, &mut errors);
    check_conditional_refs(snap, &mut errors);
    check_root_constraints(snap, &mut errors);
    check_integration_inputs(snap, &mut errors);

    errors
}

/// Every node must have a type declaration (kind != "").
fn check_declaration_before_use(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for node in &snap.nodes {
        if node.kind.is_empty() {
            errors.push(ValidationError {
                pass: PassId::Universal,
                kind: "undeclared_type".to_string(),
                entity_id: node.id,
                message: format!(
                    "Node {}@{} has no type declaration (used before full declaration)",
                    node.code, node.region.as_deref().unwrap_or("?")
                ),
            });
        }
    }
}

/// Ring IDs referenced by edges must be consistent.
fn check_ring_closure(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    // Ring closure is structurally enforced by the parser.
    // This check validates that ring IDs referenced by edges are consistent.
    let mut _ring_ids: std::collections::HashSet<String> = std::collections::HashSet::new();
    for edge in &snap.edges {
        if let Some(ref rid) = edge.ring_id {
            _ring_ids.insert(rid.clone());
        }
    }
    // TODO: validate matching open/close for each ring_id once the
    // parser emits explicit ring boundary markers.
    let _ = errors; // placeholder — no checks fire yet
}

/// Every ⊲ protocol must reference nodes that exist in the symbol table.
fn check_protocol_targets(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for edge in &snap.edges {
        if edge.rank_tag != "R2" { continue; }
        if let Some(ref label) = edge.proto_label {
            if label.contains('\u{2192}') || label.contains('\u{22A3}') {
                // label format: "CODE→CODE@REGION" or "CODE⊣CODE@REGION"
                let parts: Vec<&str> = label.splitn(2, |c: char| c == '\u{2192}' || c == '\u{22A3}')
                    .collect();
                if parts.len() == 2 {
                    let src_part = parts[0].trim();
                    let tgt_with_region = parts[1].trim();
                    let tgt_code = if let Some(at) = tgt_with_region.find('@') {
                        &tgt_with_region[..at]
                    } else {
                        tgt_with_region
                    };
                    let src_exists = snap.nodes.iter().any(|n| n.code == src_part);
                    let tgt_exists = snap.nodes.iter().any(|n| n.code == tgt_code);
                    if !src_exists || !tgt_exists {
                        errors.push(ValidationError {
                            pass: PassId::Universal,
                            kind: "protocol_target_missing".to_string(),
                            entity_id: edge.id,
                            message: format!(
                                "Protocol references edge '{}' but source({}) or target({}) node missing",
                                label, src_part, tgt_code
                            ),
                        });
                    }
                }
            }
        }
    }
}

/// Every ⊗ condition and effect must reference nodes that exist.
fn check_conditional_refs(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for tensor in &snap.tensors {
        for cond in &tensor.conditions {
            let key = format!("{}@{}", cond.code, cond.region);
            if !snap.has_node_key(&key) {
                errors.push(ValidationError {
                    pass: PassId::Universal,
                    kind: "conditional_ref_missing".to_string(),
                    entity_id: tensor.id,
                    message: format!(
                        "Conditional condition references {}@{} which is not in the symbol table",
                        cond.code, cond.region
                    ),
                });
            }
        }
        let eff_key = format!("{}@{}", tensor.effect.code, tensor.effect.region);
        if !snap.has_node_key(&eff_key) {
            errors.push(ValidationError {
                pass: PassId::Universal,
                kind: "conditional_effect_missing".to_string(),
                entity_id: tensor.id,
                message: format!(
                    "Conditional effect references {}@{} which is not in the symbol table",
                    tensor.effect.code, tensor.effect.region
                ),
            });
        }
    }
}

/// ⊙ root nodes must have Δ≠0 and must have outgoing edges.
fn check_root_constraints(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for node in &snap.nodes {
        if !node.is_root { continue; }
        let has_delta = node.state.as_ref().map_or(false, |s| {
            s.delta_val.map_or(false, |v| v != 0.0)
        });
        if !has_delta {
            errors.push(ValidationError {
                pass: PassId::Universal,
                kind: "root_no_delta".to_string(),
                entity_id: node.id,
                message: format!(
                    "Root node {}@{} must have Δ≠0",
                    node.code, node.region.as_deref().unwrap_or("?")
                ),
            });
        }
        let fanout = snap.edges.iter().any(|e| e.source_id == node.id);
        if !fanout {
            errors.push(ValidationError {
                pass: PassId::Universal,
                kind: "root_no_fanout".to_string(),
                entity_id: node.id,
                message: format!(
                    "Root node {}@{} must fan out (have outgoing edges)",
                    node.code, node.region.as_deref().unwrap_or("?")
                ),
            });
        }
    }
}

/// All ∫ integration inputs must reference existing chain signal nodes.
fn check_integration_inputs(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for node in &snap.nodes {
        if node.rank_tag != "R1" { continue; }
        if let Some(ref integ) = node.integ {
            for input in &integ.inputs {
                let key = format!("{}@{}", input.code, input.region);
                if !snap.has_node_key(&key) {
                    errors.push(ValidationError {
                        pass: PassId::Universal,
                        kind: "integ_input_missing".to_string(),
                        entity_id: node.id,
                        message: format!(
                            "Integration {} input {}@{} not found in symbol table",
                            node.code, input.code, input.region
                        ),
                    });
                }
            }
        }
    }
}
