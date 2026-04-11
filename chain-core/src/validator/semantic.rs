//! Pass 3 — Domain-specific semantic rules.
//! Currently hardcodes BioChain rules. Will eventually read from DomainPack.
//! Every function in this file is a candidate for conversion to declarative
//! data in the domain pack TOML.

use super::common::*;
use std::collections::HashSet;

/// Run all BioChain-specific semantic checks.
/// Signature takes &ProgramSnapshot now; will eventually also take &DomainPack.
pub fn check_semantic(snap: &ProgramSnapshot) -> Vec<ValidationError> {
    let mut errors = Vec::new();

    check_edge_triples(snap, &mut errors);
    check_intracellular_ordering(snap, &mut errors);
    check_vagal_relay(snap, &mut errors);
    check_gut_hormone_path(snap, &mut errors);
    check_barrier_gates(snap, &mut errors);

    errors
}

// ═══════════════════════════════════════════════════════════════════
// BioChain type category extraction (domain-specific)
// ═══════════════════════════════════════════════════════════════════

fn type_category(kind: &str) -> &str {
    if kind.starts_with("L.") || kind == "L" { return "L"; }
    if kind.starts_with("N.") || kind == "N" { return "N"; }
    if kind.starts_with("E.") || kind == "E" { return "E"; }
    if kind.starts_with("M.") || kind == "M" || kind == "Mt" { return "Mt"; }
    if kind.starts_with("Ch.") || kind == "Ch" { return "Ch"; }
    if kind.starts_with("B.") { return kind; }
    if kind.starts_with("P.") { return kind; }
    match kind {
        "R" | "Gp" | "2m" | "K" | "Ph" | "NR" | "TF" | "G" | "T" | "V" => kind,
        "Mt" => "Mt",
        _ => kind.split('.').next().unwrap_or(kind),
    }
}

// ═══════════════════════════════════════════════════════════════════
// BioChain region classification (domain-specific)
// ═══════════════════════════════════════════════════════════════════

fn is_gut_region(r: &str) -> bool {
    matches!(r, "ENS" | "GUT" | "DUODENUM" | "JEJUNUM" | "ILEUM" | "COLON" | "STOMACH")
}

fn is_cns_region(r: &str) -> bool {
    matches!(r,
        "PVN" | "LC" | "DRN" | "VTA" | "NAc" | "AMY" | "BLA" | "CeA" |
        "HPC" | "PFC" | "ACC" | "INS" | "SCN" | "PIT" | "PAG" | "RVM" |
        "EC" | "SN" | "LH" | "BST" | "POA" | "DG" | "thalamus" | "NBM" |
        "striatum" | "GPi" | "GPe" | "STN" |
        "pons" | "SLD" | "NTS" |
        "spinal" |
        "ARC" | "AP" | "ME" | "SFO" | "OVLT" |
        "HYP" | "SON" | "OFC" | "BNST" | "STR" | "THL" | "DMH" |
        "VMH" | "PUT" | "CAU" | "RN"
    )
}

fn is_cvo_region(r: &str) -> bool {
    matches!(r, "ARC" | "AP" | "ME" | "SFO" | "OVLT")
}

// ═══════════════════════════════════════════════════════════════════
// Item 1: Edge type triple validation (BioChain-specific)
// ═══════════════════════════════════════════════════════════════════

fn allowed_edge_triples() -> HashSet<(&'static str, &'static str, &'static str)> {
    vec![
        ("→", "L", "R"), ("→", "R", "Gp"), ("→", "Gp", "2m"),
        ("→", "2m", "K"), ("→", "K", "K"), ("→", "K", "TF"), ("→", "N", "N"),
        ("→", "E", "L"),
        ("→", "P.agg", "P.agg"), ("→", "P.agg", "N"), ("→", "P.agg", "Mt"),
        ("⊣", "L", "R"), ("⊣", "L", "K"), ("⊣", "K", "K"),
        ("⊣", "R", "N"), ("⊣", "2m", "K"),
        ("⊣", "P.agg", "N"), ("⊣", "P.agg", "V"), ("⊣", "P.agg", "Mt"),
        ("~>", "L", "R"), ("~>", "L", "⊲"), ("~>", "2m", "K"),
        ("~>", "E", "E"),
        ("=>", "TF", "G"), ("=>", "G", "L"), ("=>", "G", "R"), ("=>", "G", "E"),
        ("|>", "L", "T"), ("|>", "T", "E"), ("|>", "L", "V"), ("|>", "V", "L"), ("|>", "L", "E"),
        ("→", "B.beh", "B.beh"), ("⊣", "B.beh", "B.beh"),
    ].into_iter().collect()
}

fn check_edge_triples(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    let triples = allowed_edge_triples();
    for edge in &snap.edges {
        if edge.rank_tag != "R0" { continue; }
        if let Some(ref etype) = edge.edge_type {
            if let (Some(src), Some(tgt)) = (snap.node(edge.source_id), snap.node(edge.target_id)) {
                let src_cat = type_category(&src.kind);
                let tgt_cat = type_category(&tgt.kind);
                if !triples.contains(&(etype.as_str(), src_cat, tgt_cat)) {
                    errors.push(ValidationError {
                        pass: PassId::Semantic,
                        kind: "invalid_edge_type".to_string(),
                        entity_id: edge.id,
                        message: format!(
                            "Edge {} {}→{} not allowed ({}:{} {} {}:{})",
                            edge.id, edge.source_id, edge.target_id,
                            src_cat, src.code, etype, tgt_cat, tgt.code
                        ),
                    });
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// Item 2: Intracellular cascade ordering (BioChain-specific)
// L→R→Gp→2m→K — no skips allowed
// ═══════════════════════════════════════════════════════════════════

fn check_intracellular_ordering(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    let skip_violations: &[(&str, &str)] = &[
        ("L", "Gp"), ("L", "2m"), ("L", "K"),
        ("R", "2m"), ("R", "K"),
        ("Gp", "K"),
    ];
    for edge in &snap.edges {
        if edge.rank_tag != "R0" { continue; }
        if let Some(ref etype) = edge.edge_type {
            if etype != "→" { continue; }
            if let (Some(src), Some(tgt)) = (snap.node(edge.source_id), snap.node(edge.target_id)) {
                let src_cat = type_category(&src.kind);
                let tgt_cat = type_category(&tgt.kind);

                // allow steroid path: L.h→NR→TF→G
                if src.kind.starts_with("L.h") && tgt_cat == "NR" { continue; }
                if src_cat == "NR" && tgt_cat == "TF" { continue; }
                if src_cat == "TF" && tgt_cat == "G" { continue; }

                // allow ionotropic: R(coup:ion)→2m directly
                if src_cat == "R" && tgt_cat == "2m" {
                    let has_ion_coup = src.props.iter().any(|p| {
                        p.k == "coup" && (p.v == "ion" || p.v.contains("Ca") || p.v.contains("Cl") || p.v.contains("Na"))
                    });
                    if has_ion_coup { continue; }
                }

                for &(from, to) in skip_violations {
                    if src_cat == from && tgt_cat == to {
                        errors.push(ValidationError {
                            pass: PassId::Semantic,
                            kind: "intracellular_skip".to_string(),
                            entity_id: edge.id,
                            message: format!(
                                "Intracellular cascade skip: {}({})→{}({}), must go L→R→Gp→2m→K",
                                src_cat, src.code, tgt_cat, tgt.code
                            ),
                        });
                    }
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// Item 9: Vagal relay enforcement (BioChain-specific)
// ═══════════════════════════════════════════════════════════════════

fn check_vagal_relay(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for edge in &snap.edges {
        if edge.rank_tag != "R0" { continue; }
        if let (Some(src), Some(tgt)) = (snap.node(edge.source_id), snap.node(edge.target_id)) {
            let src_r = src.region.as_deref().unwrap_or("");
            let tgt_r = tgt.region.as_deref().unwrap_or("");
            if !is_gut_region(src_r) || !is_cns_region(tgt_r) { continue; }
            if src.kind.starts_with("L.h") && is_cvo_region(tgt_r) { continue; }
            errors.push(ValidationError {
                pass: PassId::Semantic,
                kind: "vagal_relay_missing".to_string(),
                entity_id: edge.id,
                message: format!(
                    "Direct gut→CNS edge: {}@{}→{}@{}, must path through VAG or humoral route (L.h→R@CVO)",
                    src.code, src_r, tgt.code, tgt_r
                ),
            });
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// Item 10: Gut hormone path enforcement (BioChain-specific)
// ═══════════════════════════════════════════════════════════════════

fn check_gut_hormone_path(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for edge in &snap.edges {
        if edge.rank_tag != "R0" { continue; }
        if let (Some(src), Some(tgt)) = (snap.node(edge.source_id), snap.node(edge.target_id)) {
            let tgt_r = tgt.region.as_deref().unwrap_or("");

            if src.kind == "N.eec" || src.kind.starts_with("N.eec.") {
                let tgt_is_hormone = tgt.kind.starts_with("L.h");
                let tgt_is_enteric_neuron = type_category(&tgt.kind) == "N"
                    && is_gut_region(tgt.region.as_deref().unwrap_or(""));
                if !tgt_is_hormone && !tgt_is_enteric_neuron {
                    errors.push(ValidationError {
                        pass: PassId::Semantic,
                        kind: "gut_hormone_path_violation".to_string(),
                        entity_id: edge.id,
                        message: format!(
                            "N.eec({})→{}({}) not allowed, enteroendocrine cells must signal via L.h",
                            src.code, tgt.kind, tgt.code
                        ),
                    });
                }
            }

            if src.kind.starts_with("L.h") && is_gut_region(src.region.as_deref().unwrap_or("")) {
                if is_cns_region(tgt_r) && !is_cvo_region(tgt_r) {
                    errors.push(ValidationError {
                        pass: PassId::Semantic,
                        kind: "gut_hormone_non_cvo".to_string(),
                        entity_id: edge.id,
                        message: format!(
                            "Gut hormone {}→{}@{} targets non-CVO CNS region, must reach ARC|NTS|AP",
                            src.code, tgt.code, tgt_r
                        ),
                    });
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// Item 11: Barrier gate validation (BioChain-specific)
// ═══════════════════════════════════════════════════════════════════

fn check_barrier_gates(snap: &ProgramSnapshot, errors: &mut Vec<ValidationError>) {
    for edge in &snap.edges {
        let gate = match &edge.gate {
            Some(g) => g,
            None => continue,
        };
        if !gate.node_code.starts_with("B.") { continue; }

        let barrier_key = format!("{}@{}", gate.node_code, gate.region);
        if !snap.has_node_key(&barrier_key) {
            errors.push(ValidationError {
                pass: PassId::Semantic,
                kind: "barrier_node_missing".to_string(),
                entity_id: edge.id,
                message: format!(
                    "Barrier gate references {}@{} which is not declared",
                    gate.node_code, gate.region
                ),
            });
            continue;
        }

        let barrier_idx = snap.node_by_key[&barrier_key];
        let barrier_node = &snap.nodes[barrier_idx];
        if barrier_node.state.is_none() {
            errors.push(ValidationError {
                pass: PassId::Semantic,
                kind: "barrier_no_state".to_string(),
                entity_id: edge.id,
                message: format!(
                    "Barrier node {}@{} must have a declared state (e.g., tight, leaky)",
                    gate.node_code, gate.region
                ),
            });
        }

        if let (Some(src), Some(tgt)) = (snap.node(edge.source_id), snap.node(edge.target_id)) {
            let src_r = src.region.as_deref().unwrap_or("");
            let tgt_r = tgt.region.as_deref().unwrap_or("");

            let crosses_boundary = match gate.node_code.as_str() {
                "B.gut" => {
                    (is_gut_region(src_r) && !is_gut_region(tgt_r)) ||
                    (!is_gut_region(src_r) && is_gut_region(tgt_r))
                }
                "B.bbb" => {
                    let src_behind_bbb = is_cns_region(src_r) && !is_cvo_region(src_r);
                    let tgt_behind_bbb = is_cns_region(tgt_r) && !is_cvo_region(tgt_r);
                    let src_peripheral = !is_cns_region(src_r);
                    let tgt_peripheral = !is_cns_region(tgt_r);
                    (src_behind_bbb && tgt_peripheral) || (src_peripheral && tgt_behind_bbb)
                }
                _ => true,
            };

            if !crosses_boundary {
                errors.push(ValidationError {
                    pass: PassId::Semantic,
                    kind: "barrier_no_crossing".to_string(),
                    entity_id: edge.id,
                    message: format!(
                        "Barrier gate {} on edge {}@{}→{}@{} doesn't cross a boundary",
                        gate.node_code, src.code, src_r, tgt.code, tgt_r
                    ),
                });
            }
        }
    }
}
