//! Domain pack schema — the data contract between the universal core and
//! domain-specific vocabulary/rules.
//!
//! The parser, validator (passes 2 and 3), executor, and convergence engine
//! all consume this. The schema is designed so that:
//!   - The parser needs only `node_types` and `cascade_tags` (items 1-2 from audit)
//!   - Pass 2 (vocabulary) needs everything in VocabularyPack
//!   - Pass 3 (semantics) needs everything in SemanticRules
//!   - The executor needs kinetic templates (future)
//!
//! Domain packs are loaded from TOML files at startup. The TOML schema
//! mirrors these structs exactly.

/// Everything the parser needs to be domain-opaque.
/// These replace the hardcoded KNOWN_TYPES and CASCADE_TAGS constants.
pub struct ParserVocab {
    /// Node type tokens, longest-first for prefix matching.
    /// e.g. BioChain: ["N.glia.mg", "N.glia.as", ..., "L.nt", "R", "Gp", "2m", ...]
    /// e.g. LogicChain: ["V.val", "E.evi", "P.dog", "P.ide", ..., "P", "C", "I", ...]
    pub node_types: Vec<String>,

    /// Cascade tag tokens, longest-first for prefix matching.
    /// e.g. BioChain: ["GPCR.G12", "GPCR.Gs", ..., "NUCLEAR", "RTK", ...]
    /// e.g. LogicChain: ["DEDUCTIVE", "INDUCTIVE", ..., "AUTHORITY"]
    pub cascade_tags: Vec<String>,
}

/// Everything Pass 2 (vocabulary validation) needs beyond the parser vocab.
/// Checks that every token the parser accepted as opaque is actually declared.
pub struct VocabularyPack {
    pub parser: ParserVocab,

    /// All legal region codes.
    pub region_codes: Vec<String>,

    /// Legal integration modes (e.g. ["thr", "rate", "burst", "tonic"]).
    pub integration_modes: Vec<String>,

    /// Legal observable relationship types.
    pub observable_relationships: Vec<String>,

    /// Legal dysreg flag type codes.
    pub dysreg_flag_types: Vec<String>,

    /// Legal dynamics labels for dysreg.
    pub dynamics_labels: Vec<String>,

    /// Legal node property keys and their allowed values.
    /// e.g. BioChain: "coup" → ["Gs", "Gi", "Gq", "G12", ...]
    pub property_vocabulary: Vec<PropertySlot>,

    /// Legal B.beh act names.
    pub behavior_acts: Vec<String>,

    /// Per-Δ-rank legal property names.
    /// Index 0 = Δ0 properties, 1 = Δ1, 2 = Δ2, 3 = Δ3.
    pub delta_properties: [Vec<String>; 4],

    /// Legal META structural program names (∫̃).
    pub meta_struct_programs: Vec<String>,

    /// Legal META connectivity program names (⊗̃).
    pub meta_conn_programs: Vec<String>,

    /// Legal META window types.
    pub meta_window_types: Vec<String>,

    /// Legal convergence risk type names (⊳⚠).
    pub convergence_risk_types: Vec<String>,

    /// Legal convergence flag type names (⚡).
    pub convergence_flag_types: Vec<String>,
}

/// A named property slot with its allowed values.
pub struct PropertySlot {
    pub key: String,
    pub values: Vec<String>,
}

/// Everything Pass 3 (domain semantics) needs. Declarative rules expressed
/// as data. Code paths are the exception.
pub struct SemanticRules {
    /// Allowed edge type triples: (edge_op, source_type_category, target_type_category).
    /// Replaces `allowed_edge_triples()` in the validator.
    pub edge_triples: Vec<EdgeTriple>,

    /// Per-cascade-tag structural constraints.
    /// e.g. "GPCR.Gs" requires receptor coupling=Gs, Gp=Gs, 2m=cAMP.
    /// e.g. "DEDUCTIVE" requires node type I with rigor=formal.
    pub cascade_rules: Vec<CascadeRule>,

    /// Named region groups for boundary rule enforcement.
    /// e.g. BioChain: "gut" → ["ENS", "GUT", ...], "cvo" → ["ARC", "AP", ...]
    /// e.g. LogicChain: (none, or "memory" → ["WM", "LTM", ...])
    pub region_groups: Vec<RegionGroup>,

    /// Boundary rules: topology constraints between region groups.
    /// e.g. BioChain: "signals from gut to cns must path through VAG or L.h→R@CVO"
    pub boundary_rules: Vec<BoundaryRule>,

    /// Type category extraction rules.
    /// e.g. "L.nt" → category "L", "P.agg" → category "P.agg" (self)
    pub type_categories: Vec<TypeCategory>,
}

pub struct EdgeTriple {
    pub edge_op: String,
    pub source_category: String,
    pub target_category: String,
}

pub struct CascadeRule {
    pub tag: String,
    /// Structural constraints expressed as key-value requirements.
    /// Interpreted by the validator, not arbitrary code.
    pub constraints: Vec<CascadeConstraint>,
}

pub enum CascadeConstraint {
    /// Node type sequence must match template (e.g. "L→R→Gp→2m→K").
    ChainShape(String),
    /// A specific node type must be present in the chain.
    RequiresNodeType(String),
    /// A specific property must have a specific value.
    RequiresProperty { key: String, value: String },
}

pub struct RegionGroup {
    pub name: String,
    pub regions: Vec<String>,
}

pub struct BoundaryRule {
    pub name: String,
    pub description: String,
    /// Source region group name.
    pub from_group: String,
    /// Target region group name.
    pub to_group: String,
    /// What's required: "relay_through" node type, or "allow_via" conditions.
    pub enforcement: BoundaryEnforcement,
}

pub enum BoundaryEnforcement {
    /// Edges must relay through a specific region (e.g. VAG for gut→cns).
    RelayThrough { via_region: String },
    /// Edges allowed only if target is in a specific region group (e.g. CVO).
    AllowOnlyToGroup { group: String },
}

pub struct TypeCategory {
    /// Prefix to match (e.g. "L." or "N." or exact "R").
    pub prefix: String,
    /// Category it maps to (e.g. "L" or "N" or "R").
    pub category: String,
}

/// The complete domain pack. Loaded from a single TOML file.
pub struct DomainPack {
    pub name: String,
    pub description: String,
    pub vocabulary: VocabularyPack,
    pub rules: SemanticRules,
}
