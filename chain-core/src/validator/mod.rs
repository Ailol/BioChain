pub mod common;
pub mod universal;
pub mod vocabulary;
pub mod semantic;

pub use common::{ValidationError, ValidationReport, PassId, ProgramSnapshot};

use spacetimedb::{reducer, ReducerContext, Table};
use crate::db::base::tables::*;

/// Run all validation passes: universal → vocabulary → semantic.
/// Short-circuits on first pass failure.
pub fn validate_program(ctx: &ReducerContext, program_id: u64) -> ValidationReport {
    let snap = ProgramSnapshot::collect(ctx, program_id);
    let mut report = ValidationReport::default();

    // Pass 1: Universal closure invariants (domain-agnostic)
    report.universal = universal::check_universal(&snap);
    if !report.universal.is_empty() {
        report.halted_at = Some(PassId::Universal);
        return report;
    }

    // Pass 2: Vocabulary validation (stub — not yet implemented)
    report.vocabulary = Vec::new(); // vocabulary::check_vocabulary(&snap, &pack)

    // Pass 3: Domain-specific semantic rules (currently hardcoded BioChain)
    report.semantic = semantic::check_semantic(&snap);
    if !report.semantic.is_empty() {
        report.halted_at = Some(PassId::Semantic);
    }

    report
}

/// SpacetimeDB reducer entry point for validation.
#[reducer]
pub fn validate(ctx: &ReducerContext, program_id: u64) -> Result<(), String> {
    let report = validate_program(ctx, program_id);

    if report.is_ok() {
        log::info!("VALIDATE|program:{}|OK", program_id);
        Ok(())
    } else {
        for e in report.all_errors() {
            log::warn!("VALIDATE|{}:{}|entity:{}|{}", e.pass, e.kind, e.entity_id, e.message);
        }
        // store validation errors as diagnostics
        for e in report.all_errors() {
            ctx.db.diag().insert(Diag {
                id: 0,
                program_id,
                kind: format!("validation:{}:{}", e.pass, e.kind),
                name: None,
                expr: e.message.clone(),
                detail: Vec::new(),
            });
        }
        let halted = match &report.halted_at {
            Some(pass) => format!(" (halted at {})", pass),
            None => String::new(),
        };
        Err(format!("{} validation errors found{}", report.error_count(), halted))
    }
}
