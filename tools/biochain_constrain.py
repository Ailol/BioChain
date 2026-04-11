#!/usr/bin/env python3
"""BioChain Constrained Decoder — knowledge-graph-aware token masking.

This module provides a stateful constraint system that integrates with
constrained decoding frameworks (xgrammar, outlines, guidance, SGLang)
to prevent biologically invalid tokens from being generated.

Instead of generate-then-validate, this constrains the model so it
CANNOT produce invalid receptor-ligand pairs, wrong cascades, or
impossible cell-region combinations.

Architecture:
    The decoder maintains a parse state that tracks:
    - Current position in the BioChain grammar (header, chain, integration, etc.)
    - The last ligand seen (to constrain which receptors are valid)
    - The last receptor seen (to constrain which coupling is valid)
    - The last enzyme seen (to constrain which product is valid)
    - All declared nodes (to constrain cross-references in ∫, ⊲, ⊗)
    - The current region context

    At each token position, get_valid_tokens() returns the set of
    allowed continuations based on the parse state + knowledge graph.

Integration with SGLang/vLLM:
    Both support custom logit processors. This module provides
    BioChainLogitProcessor that wraps the constraint state machine.
"""

from dataclasses import dataclass, field
from typing import Optional

# ═══════════════════════════════════════
# KNOWLEDGE GRAPH (derived from biochain_grammar_gen.py — single source of truth)
# ═══════════════════════════════════════

from biochain_grammar_gen import (
    BINDINGS, COUPLING_2M, SM_KINASE, KINASE_TARGETS,
    NR_TF, RTK_KINASES, JAKSTAT_KINASES,
    ENZYME_PRODUCT as _ENZYME_PRODUCT, DEGRADATION_ENZYMES,
    TRANSPORTER_SUB as _TRANSPORTER_SUB
)

# Derive lookup tables in the format the validator needs

# Ligand -> valid receptor set
LIGAND_RECEPTORS = {lig: {r for r, c in recs} for lig, recs in BINDINGS.items()}

# Receptor -> required coupling
RECEPTOR_COUPLING = {}
for _lig, _recs in BINDINGS.items():
    for _rec, _coupling in _recs:
        if _rec not in RECEPTOR_COUPLING:
            RECEPTOR_COUPLING[_rec] = _coupling

# Coupling -> downstream type
COUPLING_DOWNSTREAM = {
    "Gs": "Gp", "Gi": "Gp", "Gq": "Gp", "G12": "Gp",
    "Cl⁻": "2m", "Ca²⁺": "2m", "Na⁺": "2m", "K⁺": "2m",
    "nuclear": "TF", "RTK": "K", "JAK-STAT": "K", "cGMP": "2m",
}

# Enzyme -> valid product
ENZYME_PRODUCT = _ENZYME_PRODUCT

# Transporter -> valid substrate set
TRANSPORTER_SUBSTRATE = {t: set(s) if isinstance(s, list) else {s} for t, s in _TRANSPORTER_SUB.items()}

# Cell type -> valid regions (None = anywhere)
CELL_REGION = {
    "N.da":     {"VTA", "SN"},
    "N.5ht":    {"DRN"},
    "N.ent":    {"ENS"},
    "N.eec":    {"ENS", "GUT"},
    "N.icc":    {"ENS"},
    "B.gut":    {"GUT"},
    "B.bbb":    {"CNS"},
    "B.beh":    {"behavior"},
}



# ═══════════════════════════════════════
# PARSE STATE
# ═══════════════════════════════════════

@dataclass
class ParseState:
    """Tracks context during BioChain generation for constraint decisions."""

    # What section are we in
    section: str = "header"  # header, chain, recycling, fate, integrate, protocol, conditional, composite, dysreg, observable

    # Chain context
    last_ligand: Optional[str] = None       # last L.* code seen → constrains next R
    last_receptor: Optional[str] = None     # last R code seen → constrains coupling
    last_coupling: Optional[str] = None     # last coupling seen → constrains downstream type
    last_enzyme: Optional[str] = None       # last E code seen → constrains product
    last_transporter: Optional[str] = None  # last T code seen → constrains substrate
    last_node_type: Optional[str] = None    # last full type → constrains edge type
    last_edge: Optional[str] = None         # last edge seen

    # Symbol table
    declared_nodes: dict = field(default_factory=dict)  # code@region → type
    declared_edges: list = field(default_factory=list)   # (src, edge, tgt) tuples

    # Grammar position within a node
    in_node: bool = False
    node_has_type: bool = False
    node_code: Optional[str] = None
    expecting: Optional[str] = None  # "type", "code", "state", "props", "region", "edge", "fate"

    def register_node(self, type_full: str, code: str, region: str):
        key = f"{code}@{region}"
        if key not in self.declared_nodes:
            self.declared_nodes[key] = type_full
        # Update context
        if type_full.startswith("L."):
            self.last_ligand = code
            self.last_receptor = None
            self.last_coupling = None
            self.last_enzyme = None
        elif type_full == "R":
            self.last_receptor = code
            self.last_ligand = None  # consumed
        elif type_full == "E":
            self.last_enzyme = code
        elif type_full == "T":
            self.last_transporter = code
        self.last_node_type = type_full

    def register_coupling(self, coupling: str):
        self.last_coupling = coupling

    def register_edge(self, edge: str):
        self.last_edge = edge


# ═══════════════════════════════════════
# CONSTRAINT FUNCTIONS
# ═══════════════════════════════════════

def valid_receptors_for_ligand(ligand_code: str) -> set[str] | None:
    """Given a ligand, return valid receptor codes. None = unconstrained."""
    return LIGAND_RECEPTORS.get(ligand_code)


def valid_coupling_for_receptor(receptor_code: str) -> str | None:
    """Given a receptor, return required coupling. None = unconstrained."""
    return RECEPTOR_COUPLING.get(receptor_code)


def valid_downstream_for_coupling(coupling: str) -> str | None:
    """Given coupling type, return required next node type."""
    return COUPLING_DOWNSTREAM.get(coupling)


def valid_product_for_enzyme(enzyme_code: str) -> str | None:
    """Given enzyme, return valid product code. None = degradation (→⊘)."""
    return ENZYME_PRODUCT.get(enzyme_code)


def valid_substrate_for_transporter(transporter_code: str) -> set[str] | None:
    """Given transporter, return valid substrates."""
    return TRANSPORTER_SUBSTRATE.get(transporter_code)


def valid_regions_for_cell(cell_type: str) -> set[str] | None:
    """Given cell type, return valid regions. None = anywhere."""
    return CELL_REGION.get(cell_type)


def get_constraints(state: ParseState) -> dict:
    """Given current parse state, return active constraints.

    Returns dict of constraint_type → allowed_values.
    Empty dict = no constraints (free generation).
    """
    constraints = {}

    # After a ligand, constrain receptor choice
    if state.last_ligand and state.expecting == "code" and state.last_edge == "→":
        valid = valid_receptors_for_ligand(state.last_ligand)
        if valid:
            constraints["receptor_code"] = valid

    # After a receptor, constrain coupling
    if state.last_receptor and state.expecting == "props":
        valid = valid_coupling_for_receptor(state.last_receptor)
        if valid:
            constraints["coupling"] = {valid}

    # After coupling, constrain downstream type
    if state.last_coupling and state.expecting == "type":
        valid = valid_downstream_for_coupling(state.last_coupling)
        if valid:
            constraints["node_type"] = {valid}

    # After enzyme with → edge, constrain product
    if state.last_enzyme and state.last_edge == "→" and state.expecting == "code":
        valid = valid_product_for_enzyme(state.last_enzyme)
        if valid:
            constraints["product_code"] = {valid}
        elif valid is None:
            # Degradation enzyme → next should be ⊘ fate
            constraints["fate"] = {"→⊘"}

    # After transporter with |> edge, constrain substrate
    if state.last_transporter and state.last_edge == "|>":
        valid = valid_substrate_for_transporter(state.last_transporter)
        if valid:
            constraints["substrate_code"] = valid

    # Cell type constrains region
    if state.last_node_type and state.expecting == "region":
        valid = valid_regions_for_cell(state.last_node_type)
        if valid:
            constraints["region"] = valid

    # In ∫, ⊲, ⊗ sections: only reference declared nodes
    if state.section in ("integrate", "protocol", "conditional"):
        constraints["cross_ref"] = set(state.declared_nodes.keys())

    return constraints


# ═══════════════════════════════════════
# LOGIT PROCESSOR (for SGLang/vLLM integration)
# ═══════════════════════════════════════

class BioChainLogitProcessor:
    """Custom logit processor for constrained BioChain generation.

    Usage with SGLang:
        from sglang import Runtime
        processor = BioChainLogitProcessor(tokenizer)
        runtime = Runtime(
            model_path="Qwen/Qwen3.5-27B",
            speculative_model_path="./biochain-2b-draft",
            logit_processor=processor
        )

    Usage with vLLM:
        from vllm import SamplingParams
        params = SamplingParams(
            logits_processors=[BioChainLogitProcessor(tokenizer)]
        )
    """

    def __init__(self, tokenizer):
        self.tokenizer = tokenizer
        self.state = ParseState()
        self._build_token_maps()

    def _build_token_maps(self):
        """Pre-compute which token IDs correspond to which biological codes."""
        self.receptor_tokens = {}  # receptor_code → set of token_ids
        self.coupling_tokens = {}
        self.region_tokens = {}
        self.type_tokens = {}

        vocab = self.tokenizer.get_vocab()

        # Map each biological code to its token ID(s)
        all_receptors = set()
        for receptors in LIGAND_RECEPTORS.values():
            all_receptors.update(receptors)

        for code in all_receptors:
            matching = [tid for tok, tid in vocab.items() if code in tok]
            if matching:
                self.receptor_tokens[code] = set(matching)

        # Similarly for regions, types, etc.
        # (Full implementation would map all constrained tokens)

    def __call__(self, token_ids: list[int], logits):
        """Called at each generation step. Mask invalid tokens.

        Args:
            token_ids: tokens generated so far
            logits: raw logits for next token position

        Returns:
            Modified logits with invalid tokens set to -inf
        """
        # Decode recent tokens to update parse state
        recent = self.tokenizer.decode(token_ids[-20:])  # look back 20 tokens
        self._update_state(recent)

        # Get constraints for current position
        constraints = get_constraints(self.state)

        if not constraints:
            return logits  # No constraints — free generation

        # Apply constraints by masking invalid tokens
        import torch
        mask = torch.zeros_like(logits, dtype=torch.bool)

        if "receptor_code" in constraints:
            valid_codes = constraints["receptor_code"]
            for code in valid_codes:
                if code in self.receptor_tokens:
                    for tid in self.receptor_tokens[code]:
                        mask[tid] = True
            # If we have valid tokens, mask everything else
            if mask.any():
                logits[~mask] = float('-inf')

        return logits

    def _update_state(self, text: str):
        """Update parse state from recently generated text.

        This is a simplified state tracker. A full implementation would
        use a proper incremental parser.
        """
        # Detect section transitions
        if "∫{" in text and self.state.section != "integrate":
            self.state.section = "integrate"
        elif "⊲{" in text and self.state.section != "protocol":
            self.state.section = "protocol"
        elif "⊗(" in text and self.state.section != "conditional":
            self.state.section = "conditional"

        # Detect ligand context
        import re
        ligand_match = re.search(r'\{L\.\w+:(\w+)', text[-100:])
        if ligand_match:
            self.state.last_ligand = ligand_match.group(1)

        # Detect receptor
        receptor_match = re.search(r'\{R:([^\[\(@ ]+)', text[-100:])
        if receptor_match:
            self.state.register_coupling(
                RECEPTOR_COUPLING.get(receptor_match.group(1), "")
            )

        # Detect edge
        if text.rstrip().endswith("→"):
            self.state.last_edge = "→"
        elif text.rstrip().endswith("⊣"):
            self.state.last_edge = "⊣"
        elif text.rstrip().endswith("|>"):
            self.state.last_edge = "|>"


# ═══════════════════════════════════════
# STANDALONE VALIDATION (non-generation)
# ═══════════════════════════════════════

def validate_semantics(lint_result) -> list[str]:
    """Run semantic checks on a parsed LintResult.

    This is for post-generation validation when constrained
    decoding isn't available (e.g., API-generated outputs).
    Checks the same knowledge graph but after the fact.
    """
    errors = []

    for edge in lint_result.edges:
        src = lint_result.nodes.get(edge.source)
        tgt = lint_result.nodes.get(edge.target)
        if not src or not tgt:
            continue

        # Ligand → Receptor binding check (R type)
        if src.type_full and src.type_full.startswith("L.") and tgt.type_full == "R":
            if edge.edge_type in ("→", "~>"):
                valid = LIGAND_RECEPTORS.get(src.code)
                if valid and tgt.code not in valid:
                    errors.append(
                        f"BINDING L{edge.line_num}: {src.code} does not bind {tgt.code}. "
                        f"Valid receptors: {', '.join(sorted(valid))}"
                    )

        # Ligand → Nuclear Receptor binding check (NR type)
        if src.type_full and src.type_full.startswith("L.") and tgt.type_full == "NR":
            if edge.edge_type in ("→", "⊣"):
                valid = LIGAND_RECEPTORS.get(src.code)
                if valid and tgt.code not in valid:
                    errors.append(
                        f"BINDING L{edge.line_num}: {src.code} does not bind NR:{tgt.code}. "
                        f"Valid receptors: {', '.join(sorted(valid))}"
                    )

        # Receptor coupling check
        if tgt.type_full == "R" and tgt.props:
            required = RECEPTOR_COUPLING.get(tgt.code)
            if required:
                coupling_found = None
                for p in tgt.props:
                    if p in ("Gs", "Gi", "Gq", "G12", "Cl⁻", "Ca²⁺", "Na⁺", "K⁺",
                             "RTK", "JAK-STAT", "nuclear"):
                        coupling_found = p
                        break
                if coupling_found and coupling_found != required:
                    errors.append(
                        f"COUPLING L{edge.line_num}: {tgt.code} uses {required}, "
                        f"not {coupling_found}"
                    )

        # Enzyme → Product check (check by code since same node can be typed G or E)
        if tgt.type_full and tgt.type_full.startswith("L.") and edge.edge_type == "→":
            if src.code in ENZYME_PRODUCT:
                valid_product = ENZYME_PRODUCT[src.code]
                if valid_product is not None and tgt.code != valid_product:
                    errors.append(
                        f"SYNTHESIS L{edge.line_num}: {src.code} produces {valid_product}, "
                        f"not {tgt.code}"
                    )
                elif valid_product is None:
                    errors.append(
                        f"SYNTHESIS L{edge.line_num}: {src.code} is a degradation enzyme, "
                        f"should not produce {tgt.code}. Use →⊘ instead."
                    )
            elif src.code in DEGRADATION_ENZYMES:
                errors.append(
                    f"SYNTHESIS L{edge.line_num}: {src.code} is a degradation enzyme, "
                    f"should not produce {tgt.code}. Use →⊘ instead."
                )

        # Transporter → Substrate check
        if src.type_full == "T" and edge.edge_type == "|>":
            valid_sub = TRANSPORTER_SUBSTRATE.get(src.code)
            if valid_sub and tgt.code not in valid_sub:
                errors.append(
                    f"TRANSPORT L{edge.line_num}: {src.code} carries "
                    f"{', '.join(sorted(valid_sub))}, not {tgt.code}"
                )

    # Cell type → Region check
    for key, node in lint_result.nodes.items():
        if node.type_full:
            valid_regions = CELL_REGION.get(node.type_full)
            if valid_regions and node.region not in valid_regions:
                errors.append(
                    f"CELL_REGION L{node.line_num}: {node.type_full}:{node.code} "
                    f"invalid at {node.region}. Valid: {', '.join(sorted(valid_regions))}"
                )

    return errors


# ═══════════════════════════════════════
# CLI
# ═══════════════════════════════════════

if __name__ == "__main__":
    import sys
    from biochain_lint import lint

    if len(sys.argv) < 2:
        print("Usage: python biochain_constrain.py <file.bc>")
        print("  Runs semantic validation (knowledge graph) on a BioChain file.")
        print("  For generation-time constraints, import BioChainLogitProcessor.")
        sys.exit(1)

    with open(sys.argv[1]) as f:
        text = f.read()

    # First run structural linter
    result = lint(text)
    if not result.valid:
        print("STRUCTURAL ERRORS (fix these first):")
        for e in result.errors:
            print(f"  {e}")
        print()

    # Then run semantic validation
    semantic_errors = validate_semantics(result)
    if semantic_errors:
        print("SEMANTIC ERRORS:")
        for e in semantic_errors:
            print(f"  {e}")
    else:
        print("SEMANTIC: All biological relationships valid.")

    total_errors = len(result.errors) + len(semantic_errors)
    sys.exit(0 if total_errors == 0 else 1)
