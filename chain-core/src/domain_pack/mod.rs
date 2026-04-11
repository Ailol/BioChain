/// A domain pack supplies vocabulary, region codes, cascade tags,
/// and semantic rules for a specific Chain domain.
///
/// The core parser parameterizes its terminal sets from the loaded domain pack.
/// The core validator runs universal closure checks unconditionally and
/// domain-specific checks by dispatching to handlers declared in the pack.
///
/// Domain packs are data, not code. Declarative rules of the form
/// "given construct X with property Y, require Z" expressed as data.
/// Code paths are the exception, reserved for genuinely procedural checks.
pub trait DomainPack {
    /// Domain name (e.g. "biochain", "logicchain", "orgchain")
    fn name(&self) -> &str;

    /// Legal node type tokens (e.g. ["L.nt", "R", "Gp", "2m", "K", ...])
    fn node_types(&self) -> &[&str];

    /// Legal region codes (e.g. ["PFC", "AMY", "HPC", ...])
    fn region_codes(&self) -> &[&str];

    /// Legal cascade tags (e.g. ["GPCR.Gs", "NUCLEAR", "RTK", ...])
    fn cascade_tags(&self) -> &[&str];

    /// Legal integration modes (e.g. ["thr", "rate", "burst", "tonic"])
    fn integration_modes(&self) -> &[&str];

    /// Legal observable relationship types (e.g. ["direct", "proxy", "ratio", ...])
    fn observable_relationships(&self) -> &[&str];

    /// Legal dysreg flag types (e.g. ["sus", "dep", "exc", ...])
    fn dysreg_flag_types(&self) -> &[&str];

    /// Legal dynamics labels (e.g. ["positive_feedback_dominant", ...])
    fn dynamics_labels(&self) -> &[&str];

    // TODO: cascade coupling rules (pass 3 semantic validation)
    // TODO: kinetic templates (executor forward simulation)
    // TODO: Δ property vocabulary per rank
    // TODO: META structural program enums
}
