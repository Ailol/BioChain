use crate::db::base::tables::*;
use crate::db::plasticity::tables::*;
use crate::db::meta::tables::*;
use crate::db::convergence::tables::*;
use spacetimedb::ReducerContext;
use std::collections::HashMap;

// ═══════════════════════════════════════════════════════════════════
// Shared validation types
// ═══════════════════════════════════════════════════════════════════

#[derive(Clone, Debug)]
pub struct ValidationError {
    pub pass: PassId,
    pub kind: String,
    pub entity_id: u64,
    pub message: String,
}

#[derive(Clone, Debug, PartialEq)]
pub enum PassId {
    Universal,
    Vocabulary,
    Semantic,
}

impl std::fmt::Display for PassId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            PassId::Universal => write!(f, "universal"),
            PassId::Vocabulary => write!(f, "vocabulary"),
            PassId::Semantic => write!(f, "semantic"),
        }
    }
}

/// Result of the full validation pipeline.
#[derive(Clone, Debug, Default)]
pub struct ValidationReport {
    pub universal: Vec<ValidationError>,
    pub vocabulary: Vec<ValidationError>,
    pub semantic: Vec<ValidationError>,
    /// Which pass halted the pipeline (None = all passes ran).
    pub halted_at: Option<PassId>,
}

impl ValidationReport {
    pub fn is_ok(&self) -> bool {
        self.universal.is_empty() && self.vocabulary.is_empty() && self.semantic.is_empty()
    }

    pub fn all_errors(&self) -> Vec<&ValidationError> {
        self.universal.iter()
            .chain(self.vocabulary.iter())
            .chain(self.semantic.iter())
            .collect()
    }

    pub fn error_count(&self) -> usize {
        self.universal.len() + self.vocabulary.len() + self.semantic.len()
    }
}

// ═══════════════════════════════════════════════════════════════════
// Shared graph snapshot — collected once, consumed by all passes
// ═══════════════════════════════════════════════════════════════════

pub struct ProgramSnapshot {
    // BASE
    pub nodes: Vec<Node>,
    pub edges: Vec<Edge>,
    pub tensors: Vec<Tensor>,
    pub node_by_id: HashMap<u64, usize>,
    pub node_by_key: HashMap<String, usize>, // "code@region" → index in nodes
    // PLASTICITY
    pub delta_ops: Vec<DeltaOp>,
    // META
    pub meta_ops: Vec<MetaOp>,
    // CONVERGENCE
    pub convs: Vec<Conv>,
}

impl ProgramSnapshot {
    pub fn collect(ctx: &ReducerContext, program_id: u64) -> Self {
        let nodes: Vec<Node> = ctx.db.node().by_program().filter(program_id).collect();
        let edges: Vec<Edge> = ctx.db.edge().by_program().filter(program_id).collect();
        let tensors: Vec<Tensor> = ctx.db.tensor().by_program().filter(program_id).collect();
        let delta_ops: Vec<DeltaOp> = ctx.db.delta_op().by_program().filter(program_id).collect();
        let meta_ops: Vec<MetaOp> = ctx.db.meta_op().by_program().filter(program_id).collect();
        let convs: Vec<Conv> = ctx.db.conv().by_program().filter(program_id).collect();

        let node_by_id: HashMap<u64, usize> = nodes.iter().enumerate()
            .map(|(i, n)| (n.id, i)).collect();
        let node_by_key: HashMap<String, usize> = nodes.iter().enumerate()
            .map(|(i, n)| {
                let key = format!("{}@{}", n.code, n.region.as_deref().unwrap_or(""));
                (key, i)
            }).collect();

        Self { nodes, edges, tensors, node_by_id, node_by_key, delta_ops, meta_ops, convs }
    }

    /// Build a snapshot from pre-collected data (for testing without SpacetimeDB).
    #[cfg(test)]
    pub fn from_parts(
        nodes: Vec<Node>, edges: Vec<Edge>, tensors: Vec<Tensor>,
        delta_ops: Vec<DeltaOp>, meta_ops: Vec<MetaOp>, convs: Vec<Conv>,
    ) -> Self {
        let node_by_id: HashMap<u64, usize> = nodes.iter().enumerate()
            .map(|(i, n)| (n.id, i)).collect();
        let node_by_key: HashMap<String, usize> = nodes.iter().enumerate()
            .map(|(i, n)| {
                let key = format!("{}@{}", n.code, n.region.as_deref().unwrap_or(""));
                (key, i)
            }).collect();
        Self { nodes, edges, tensors, node_by_id, node_by_key, delta_ops, meta_ops, convs }
    }

    pub fn node(&self, id: u64) -> Option<&Node> {
        self.node_by_id.get(&id).map(|&i| &self.nodes[i])
    }

    pub fn has_node_key(&self, key: &str) -> bool {
        self.node_by_key.contains_key(key)
    }
}
