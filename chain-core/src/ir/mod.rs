// TODO: Universal IR — the single canonical intermediate representation.
//
// Same IR shape for every Chain. The IR is a typed graph with:
//   - Stable IDs
//   - Nodes carrying their universal kind (declared by domain pack, not hardcoded)
//   - Edges carrying operator and sign
//   - ∫/⊲/⊗ as first-class graph nodes with input/output ports
//   - Δs as labeled diff entries pointing at IR node IDs
//   - σ̃/⊲̃/∫̃/⊗̃ as constraint annotations attached to IR nodes or edges
//   - ∮/⊳/⊳⚠/⚡/⊕⊳ as analysis records attached to IR nodes
//
// The IR has zero domain knowledge in its schema. The semantics of "what a
// node means" lives in the domain pack and only matters when you go from IR
// to a backend (simulator, visualizer, query engine).
//
// The IR doesn't need to understand the domain to be useful for diff, query,
// validation, storage, and visualization. Only forward simulation needs
// domain plugins (kinetic templates per cascade tag).
