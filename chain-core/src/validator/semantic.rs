// TODO: Pass 3 — Domain-specific semantic rules dispatched through domain pack.
//
// Declarative pattern-matching rules of the form:
//   "given construct X with property Y, require Z"
// expressed as data in the domain pack, not code.
//
// Examples (BioChain):
//   - GPCR.Gq cascade → receptor must have (Gq), Gp must be Gq, 2m must be IP3/DAG
//   - ENS→VAG→NTS relay enforcement
//   - Gut hormone path: N.eec→L.h→R@CVO only
//
// Examples (LogicChain):
//   - DEDUCTIVE chains must terminate in formal I
//   - BAYESIAN must have prior+evidence inputs
//   - HEURISTIC chains must carry H node
//
// Code paths reserved for genuinely procedural checks only.
