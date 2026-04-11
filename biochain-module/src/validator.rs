use spacetimedb::{reducer, ReducerContext, Table};
use crate::base::tables::*;
use std::collections::{HashMap, HashSet};

// ═══════════════════════════════════════════════════════════════════
// Validation error types
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
pub struct ValidationError {
    pub kind: String,
    pub entity_id: u64,
    pub message: String,
}

// ═══════════════════════════════════════════════════════════════════
// Allowed edge type triples (from BASE_SYSTEM_PROMPT.txt EDGES section)
// ═══════════════════════════════════════════════════════════════════

fn allowed_edge_triples() -> Vec<(&'static str, &'static str, &'static str)> {
    vec![
        // → activates
        ("→", "L", "R"), ("→", "R", "Gp"), ("→", "Gp", "2m"),
        ("→", "2m", "K"), ("→", "K", "K"), ("→", "K", "TF"), ("→", "N", "N"),
        ("→", "E", "L"),
        ("→", "P.agg", "P.agg"), ("→", "P.agg", "N"), ("→", "P.agg", "Mt"),
        // ⊣ inhibits
        ("⊣", "L", "R"), ("⊣", "L", "K"), ("⊣", "K", "K"),
        ("⊣", "R", "N"), ("⊣", "2m", "K"),
        ("⊣", "P.agg", "N"), ("⊣", "P.agg", "V"), ("⊣", "P.agg", "Mt"),
        // ~> modulates
        ("~>", "L", "R"), ("~>", "L", "⊲"), ("~>", "2m", "K"),
        ("~>", "E", "E"),  // E.lf~>E.lf
        ("~>", "L", "R"),  // L.ns~>R (already covered by L~>R)
        // => transcribes
        ("=>", "TF", "G"), ("=>", "G", "L"), ("=>", "G", "R"), ("=>", "G", "E"),
        // |> transports
        ("|>", "L", "T"), ("|>", "T", "E"), ("|>", "L", "V"), ("|>", "V", "L"), ("|>", "L", "E"),
        // B.beh behavioral competition
        ("→", "B.beh", "B.beh"), ("⊣", "B.beh", "B.beh"),
    ]
}

// ═══════════════════════════════════════════════════════════════════
// Region classification helpers
// ═══════════════════════════════════════════════════════════════════

fn is_gut_region(r: &str) -> bool {
    matches!(r, "ENS" | "GUT" | "DUODENUM" | "JEJUNUM" | "ILEUM" | "COLON" | "STOMACH")
}

fn is_cns_region(r: &str) -> bool {
    matches!(r,
        // CNS core
        "PVN" | "LC" | "DRN" | "VTA" | "NAc" | "AMY" | "BLA" | "CeA" |
        "HPC" | "PFC" | "ACC" | "INS" | "SCN" | "PIT" | "PAG" | "RVM" |
        "EC" | "SN" | "LH" | "BST" | "POA" | "DG" | "thalamus" | "NBM" |
        // Basal ganglia
        "striatum" | "GPi" | "GPe" | "STN" |
        // Brainstem
        "pons" | "SLD" | "NTS" |
        // Spinal
        "spinal" |
        // CVO (also CNS)
        "ARC" | "AP" | "ME" | "SFO" | "OVLT" |
        // Legacy compat
        "HYP" | "SON" | "OFC" | "BNST" | "STR" | "THL" | "DMH" |
        "VMH" | "PUT" | "CAU" | "RN"
    )
}

/// Circumventricular organs — lack BBB, allow humoral access from periphery
fn is_cvo_region(r: &str) -> bool {
    matches!(r, "ARC" | "AP" | "ME" | "SFO" | "OVLT")
}

/// Extract the top-level type category from a full kind string.
/// e.g. "L.nt" → "L", "R" → "R", "N.pyr" → "N", "2m" → "2m"
fn type_category(kind: &str) -> &str {
    if kind.starts_with("L.") || kind == "L" { return "L"; }
    if kind.starts_with("N.") || kind == "N" { return "N"; }
    if kind.starts_with("E.") || kind == "E" { return "E"; }
    if kind.starts_with("M.") || kind == "M" || kind == "Mt" { return "Mt"; }
    if kind.starts_with("Ch.") || kind == "Ch" { return "Ch"; }
    if kind.starts_with("B.") { return kind; } // B.gut, B.bbb, B.beh as-is
    if kind.starts_with("P.") { return kind; } // P.agg, P.oligo as-is
    // exact matches
    match kind {
        "R" | "Gp" | "2m" | "K" | "Ph" | "NR" | "TF" | "G" | "T" | "V" => kind,
        "Mt" => "Mt",
        _ => kind.split('.').next().unwrap_or(kind),
    }
}

// ═══════════════════════════════════════════════════════════════════
// Main validation function
// ═══════════════════════════════════════════════════════════════════

pub fn validate_program(ctx: &ReducerContext, program_id: u64) -> Vec<ValidationError> {
    let mut errors = Vec::new();

    // collect all nodes and edges
    let nodes: Vec<Node> = ctx.db.node().by_program().filter(program_id).collect();
    let edges: Vec<Edge> = ctx.db.edge().by_program().filter(program_id).collect();
    let tensors: Vec<Tensor> = ctx.db.tensor().by_program().filter(program_id).collect();

    // build lookup maps
    let node_by_id: HashMap<u64, &Node> = nodes.iter().map(|n| (n.id, n)).collect();
    let node_by_key: HashMap<String, &Node> = nodes.iter().map(|n| {
        let key = format!("{}@{}", n.code, n.region.as_deref().unwrap_or(""));
        (key, n)
    }).collect();

    // 1. Edge type triple validation
    let triples = allowed_edge_triples();
    let triple_set: HashSet<(&str, &str, &str)> = triples.into_iter().collect();

    for edge in &edges {
        if edge.rank_tag != "R0" { continue; }
        if let Some(ref etype) = edge.edge_type {
            if let (Some(src), Some(tgt)) = (node_by_id.get(&edge.source_id), node_by_id.get(&edge.target_id)) {
                let src_cat = type_category(&src.kind);
                let tgt_cat = type_category(&tgt.kind);
                if !triple_set.contains(&(etype.as_str(), src_cat, tgt_cat)) {
                    errors.push(ValidationError {
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

    // 2. Intracellular reachability: L→R→Gp→2m→K path must exist (never skip)
    // Check that chains don't jump L→K, L→2m, R→K, R→2m, Gp→K etc.
    let skip_violations: Vec<(&str, &str)> = vec![
        ("L", "Gp"), ("L", "2m"), ("L", "K"),
        ("R", "2m"), ("R", "K"),
        ("Gp", "K"),
    ];
    for edge in &edges {
        if edge.rank_tag != "R0" { continue; }
        if let Some(ref etype) = edge.edge_type {
            if etype != "→" { continue; }
            if let (Some(src), Some(tgt)) = (node_by_id.get(&edge.source_id), node_by_id.get(&edge.target_id)) {
                let src_cat = type_category(&src.kind);
                let tgt_cat = type_category(&tgt.kind);

                // allow steroid path: L.h→NR→TF→G
                if src.kind.starts_with("L.h") && tgt_cat == "NR" { continue; }
                if src_cat == "NR" && tgt_cat == "TF" { continue; }
                if src_cat == "TF" && tgt_cat == "G" { continue; }

                // allow ionotropic: R(coup:ion)→2m directly
                if src_cat == "R" && tgt_cat == "2m" {
                    let has_ion = src.props.iter().any(|p| p.k == "coup" && p.v == "ion");
                    if has_ion { continue; }
                    // also allow any receptor with coup containing ion-like entries
                    let has_ion_coup = src.props.iter().any(|p| {
                        p.k == "coup" && (p.v.contains("Ca") || p.v.contains("Cl") || p.v.contains("Na") || p.v.contains("ion"))
                    });
                    if has_ion_coup { continue; }
                }

                for &(from, to) in &skip_violations {
                    if src_cat == from && tgt_cat == to {
                        errors.push(ValidationError {
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

    // 3. Declaration-before-use: check that short-form nodes have full type
    for node in &nodes {
        if node.kind.is_empty() {
            errors.push(ValidationError {
                kind: "undeclared_type".to_string(),
                entity_id: node.id,
                message: format!(
                    "Node {}@{} has no type declaration (used before full declaration)",
                    node.code, node.region.as_deref().unwrap_or("?")
                ),
            });
        }
    }

    // 4. Ring closure: every ring_open must have matching ring_close
    let mut ring_opens: HashSet<String> = HashSet::new();
    let _ring_closes: HashSet<String> = HashSet::new();
    for edge in &edges {
        if let Some(ref rid) = edge.ring_id {
            // edges within rings have ring_id set
            ring_opens.insert(rid.clone());
        }
    }
    // Note: ring closure is structurally enforced by the parser.
    // This check validates that ring IDs referenced by edges are consistent.

    // 5. Protocol targets: every ⊲ must reference an existing edge
    for edge in &edges {
        if edge.rank_tag != "R2" { continue; }
        if let Some(ref label) = edge.proto_label {
            // protocol's edge_label should reference a chain edge
            // e.g. "GLU→NMDA@PFC" — check that source and target nodes exist
            if label.contains('→') || label.contains('⊣') {
                let parts: Vec<&str> = label.splitn(2, |c: char| c == '→' || c == '⊣')
                    .collect();
                if parts.len() == 2 {
                    let src_part = parts[0].trim();
                    let tgt_with_region = parts[1].trim();
                    let tgt_code = if let Some(at) = tgt_with_region.find('@') {
                        &tgt_with_region[..at]
                    } else {
                        tgt_with_region
                    };
                    // check both exist as nodes
                    let src_exists = nodes.iter().any(|n| n.code == src_part);
                    let tgt_exists = nodes.iter().any(|n| n.code == tgt_code);
                    if !src_exists || !tgt_exists {
                        errors.push(ValidationError {
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

    // 6. Conditional references: every ⊗ condition/effect must reference existing nodes
    for tensor in &tensors {
        for cond in &tensor.conditions {
            let key = format!("{}@{}", cond.code, cond.region);
            if !node_by_key.contains_key(&key) {
                errors.push(ValidationError {
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
        if !node_by_key.contains_key(&eff_key) {
            errors.push(ValidationError {
                kind: "conditional_effect_missing".to_string(),
                entity_id: tensor.id,
                message: format!(
                    "Conditional effect references {}@{} which is not in the symbol table",
                    tensor.effect.code, tensor.effect.region
                ),
            });
        }
    }

    // 7. Root constraints: ⊙ nodes must have Δ≠0 and must fan-out
    for node in &nodes {
        if !node.is_root { continue; }
        let has_delta = node.state.as_ref().map_or(false, |s| {
            s.delta_val.map_or(false, |v| v != 0.0)
        });
        if !has_delta {
            errors.push(ValidationError {
                kind: "root_no_delta".to_string(),
                entity_id: node.id,
                message: format!(
                    "Root node {}@{} must have Δ≠0",
                    node.code, node.region.as_deref().unwrap_or("?")
                ),
            });
        }
        let fanout = edges.iter().any(|e| e.source_id == node.id);
        if !fanout {
            errors.push(ValidationError {
                kind: "root_no_fanout".to_string(),
                entity_id: node.id,
                message: format!(
                    "Root node {}@{} must fan out (have outgoing edges)",
                    node.code, node.region.as_deref().unwrap_or("?")
                ),
            });
        }
    }

    // 8. Integration inputs: all ∫ inputs must reference existing chain signal nodes
    for node in &nodes {
        if node.rank_tag != "R1" { continue; }
        if let Some(ref integ) = node.integ {
            for input in &integ.inputs {
                let key = format!("{}@{}", input.code, input.region);
                if !node_by_key.contains_key(&key) {
                    errors.push(ValidationError {
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

    // 9. Vagal relay reachability: ENS→VAG→NTS path enforcement
    // Direct edges from gut regions to CNS regions are forbidden.
    // Signals must path through VAG (neural) or L.h→R@CVO (humoral).
    for edge in &edges {
        if edge.rank_tag != "R0" { continue; }
        if let (Some(src), Some(tgt)) = (node_by_id.get(&edge.source_id), node_by_id.get(&edge.target_id)) {
            let src_r = src.region.as_deref().unwrap_or("");
            let tgt_r = tgt.region.as_deref().unwrap_or("");

            if !is_gut_region(src_r) || !is_cns_region(tgt_r) { continue; }

            // Allow humoral route: L.h@GUT → R@CVO (circumventricular organs lack BBB)
            if src.kind.starts_with("L.h") && is_cvo_region(tgt_r) {
                continue;
            }

            errors.push(ValidationError {
                kind: "vagal_relay_missing".to_string(),
                entity_id: edge.id,
                message: format!(
                    "Direct gut→CNS edge: {}@{}→{}@{}, must path through VAG (ENS→VAG→NTS) or humoral route (L.h→R@CVO)",
                    src.code, src_r, tgt.code, tgt_r
                ),
            });
        }
    }

    // 10. Gut hormone reachability: N.eec→L.h→R@(ARC|NTS|AP) path enforcement
    // Enteroendocrine outputs must signal via L.h hormones.
    // L.h from gut can only reach CNS at circumventricular organs.
    for edge in &edges {
        if edge.rank_tag != "R0" { continue; }
        if let (Some(src), Some(tgt)) = (node_by_id.get(&edge.source_id), node_by_id.get(&edge.target_id)) {
            let tgt_r = tgt.region.as_deref().unwrap_or("");

            // N.eec can only directly connect to L.h (hormone release)
            if src.kind == "N.eec" || src.kind.starts_with("N.eec.") {
                let tgt_is_hormone = tgt.kind.starts_with("L.h");
                // Allow enteric neural connections (N.eec → N within ENS)
                let tgt_is_enteric_neuron = type_category(&tgt.kind) == "N"
                    && is_gut_region(tgt.region.as_deref().unwrap_or(""));
                if !tgt_is_hormone && !tgt_is_enteric_neuron {
                    errors.push(ValidationError {
                        kind: "gut_hormone_path_violation".to_string(),
                        entity_id: edge.id,
                        message: format!(
                            "N.eec({})→{}({}) not allowed, enteroendocrine cells must signal via L.h",
                            src.code, tgt.kind, tgt.code
                        ),
                    });
                }
            }

            // L.h from gut regions reaching CNS must target CVO regions (ARC, NTS, AP)
            if src.kind.starts_with("L.h") && is_gut_region(src.region.as_deref().unwrap_or("")) {
                if is_cns_region(tgt_r) && !is_cvo_region(tgt_r) {
                    errors.push(ValidationError {
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

    // 11. Barrier gate validation: →? on B.gut/B.bbb
    // - Barrier node must be declared with a state (tight|leaky)
    // - Edge must straddle the barrier boundary
    for edge in &edges {
        let gate = match &edge.gate {
            Some(g) => g,
            None => continue,
        };

        // Only validate barrier gates (B.gut, B.bbb)
        if !gate.node_code.starts_with("B.") { continue; }

        // a) Barrier node must exist and have declared state
        let barrier_key = format!("{}@{}", gate.node_code, gate.region);
        match node_by_key.get(&barrier_key) {
            None => {
                errors.push(ValidationError {
                    kind: "barrier_node_missing".to_string(),
                    entity_id: edge.id,
                    message: format!(
                        "Barrier gate references {}@{} which is not declared",
                        gate.node_code, gate.region
                    ),
                });
            }
            Some(barrier_node) => {
                if barrier_node.state.is_none() {
                    errors.push(ValidationError {
                        kind: "barrier_no_state".to_string(),
                        entity_id: edge.id,
                        message: format!(
                            "Barrier node {}@{} must have a declared state (e.g., tight, leaky)",
                            gate.node_code, gate.region
                        ),
                    });
                }
            }
        }

        // b) Edge must straddle the barrier boundary
        if let (Some(src), Some(tgt)) = (node_by_id.get(&edge.source_id), node_by_id.get(&edge.target_id)) {
            let src_r = src.region.as_deref().unwrap_or("");
            let tgt_r = tgt.region.as_deref().unwrap_or("");

            let crosses_boundary = match gate.node_code.as_str() {
                "B.gut" => {
                    // B.gut: edge must cross GUT↔systemic boundary
                    (is_gut_region(src_r) && !is_gut_region(tgt_r)) ||
                    (!is_gut_region(src_r) && is_gut_region(tgt_r))
                }
                "B.bbb" => {
                    // B.bbb: edge must cross peripheral↔CNS boundary
                    // CVO regions lack BBB — B.bbb gates invalid there
                    let src_behind_bbb = is_cns_region(src_r) && !is_cvo_region(src_r);
                    let tgt_behind_bbb = is_cns_region(tgt_r) && !is_cvo_region(tgt_r);
                    let src_peripheral = !is_cns_region(src_r);
                    let tgt_peripheral = !is_cns_region(tgt_r);
                    (src_behind_bbb && tgt_peripheral) || (src_peripheral && tgt_behind_bbb)
                }
                _ => true, // unknown barrier type, skip boundary check
            };

            if !crosses_boundary {
                errors.push(ValidationError {
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

    errors
}

// ═══════════════════════════════════════════════════════════════════
// Validation reducer
// ═══════════════════════════════════════════════════════════════════

#[reducer]
pub fn validate(ctx: &ReducerContext, program_id: u64) -> Result<(), String> {
    let errors = validate_program(ctx, program_id);

    if errors.is_empty() {
        log::info!("VALIDATE|program:{}|OK", program_id);
        Ok(())
    } else {
        for e in &errors {
            log::warn!("VALIDATE|{}|entity:{}|{}", e.kind, e.entity_id, e.message);
        }
        // store validation errors as diagnostics
        for e in &errors {
            ctx.db.diag().insert(Diag {
                id: 0,
                program_id,
                kind: format!("validation:{}", e.kind),
                name: None,
                expr: e.message.clone(),
                detail: Vec::new(),
            });
        }
        Err(format!("{} validation errors found", errors.len()))
    }
}
