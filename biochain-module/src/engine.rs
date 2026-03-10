// Placeholder -- built later when simulation is needed.
// Will read across all layers: base + plasticity + meta.
// Single reducer: engine_tick(program_id)
//
// Phase 1: R0 RESOLVE -> R1 INTEGRATE -> R2 APPLY -> R3 EVALUATE -> EMIT
// Phase 2: Δ@R0 -> Δ@R1 -> Δ@R2 -> Δ@R3 (check triggers, log fired)
// Phase 3: Convergence update (if meta populated)
