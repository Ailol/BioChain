#!/usr/bin/env python3
"""BioChain v2.5 Linter — validates BASE pipeline outputs."""

import re
import sys
from dataclasses import dataclass, field
from enum import Enum

# ── VALID ENUMS ──

VALID_STATES = {"++", "+", "=", "~", "-", "--", "X", "*"}

VALID_REGIONS = {
    # CNS
    "PVN", "LC", "DRN", "VTA", "NAc", "AMY", "BLA", "CeA", "HPC", "PFC",
    "ACC", "INS", "SCN", "PIT", "PAG", "RVM", "EC", "SN", "LH", "BST",
    "POA", "DG", "thalamus", "NBM", "CNS",
    # Basal ganglia
    "striatum", "GPi", "GPe", "STN",
    # Brainstem
    "pons", "SLD",
    # Spinal
    "spinal",
    # ENS/GUT
    "ENS", "GUT", "VAG", "NTS", "DMV", "AP",
    # Endocrine
    "ARC", "ADR", "THYROID", "GONAD",
    # Peripheral
    "LIVER", "systemic", "plasma", "kidney", "cardiac",
    # Behavioral
    "behavior",
}

VALID_CHEM_TYPES = {
    "L.nt", "L.h", "L.p", "L.cb", "L.ni", "L.ns", "L.mb",
    "R", "Gp", "2m", "K", "Ph", "NR", "TF", "G", "T", "E", "V",
}
VALID_ELEC_TYPES = {"E.v", "E.lf", "E.gj", "Ch", "Ch.vg", "Ch.mec", "Ch.trp"}
VALID_META_TYPES = {"M.atp", "M.glc", "M.ros", "M.o2", "Mt"}  # M:* extensible
VALID_STRUCT_TYPES = {
    "N.pyr", "N.da", "N.5ht", "N.gaba", "N.gran", "N.glia", "N.glia.mg",
    "N.glia.as", "N.ent", "N.eec", "N.icc", "B.gut", "B.bbb", "B.beh",
    "P.agg", "P.oligo", "N",
}
ALL_KNOWN_TYPES = VALID_CHEM_TYPES | VALID_ELEC_TYPES | VALID_META_TYPES | VALID_STRUCT_TYPES

VALID_EDGES = {"→", "⊣", "~>", "=>", "|>"}

VALID_COUPLINGS = {"Gs", "Gi", "Gq", "G12", "Cl⁻", "Ca²⁺", "Na⁺", "K⁺", "RTK", "JAK-STAT", "heteromer"}

VALID_FATES = {"↺⁺", "↺⁻", "↺⁰", "→⊘", "→□", "→≋", "→Δm"}

VALID_INTEGRATE_SIGNS = {"+", "-", "×"}
VALID_INTEGRATE_MODES = {"thr", "rate", "burst", "tonic"}

VALID_PROTOCOL_TERMS = {"exc", "inh", "mod", "fast", "slow", "tonic", "syn", "vol", "gap", "para"}

VALID_DYSREG_TYPES = {"sus", "dep", "exc", "shunt", "osc", "res", "acc", "lock", "sat"}

VALID_OBSERVABLE_RELS = {"direct", "proxy", "ratio", "activity", "metabolite", "autonomic"}

LIGAND_PREFIXES = {"L.nt", "L.h", "L.p", "L.cb", "L.ni", "L.ns", "L.mb"}

# Type classes for cascade checking
GPCR_COUPLINGS = {"Gs", "Gi", "Gq", "G12"}
ION_COUPLINGS = {"Cl⁻", "Ca²⁺", "Na⁺", "K⁺"}

# ── DATA STRUCTURES ──

class Severity(Enum):
    ERROR = "ERROR"
    WARN = "WARN"

@dataclass
class Issue:
    severity: Severity
    check: str
    line_num: int
    message: str

    def __str__(self):
        return f"{self.severity.value} [{self.check}] L{self.line_num}: {self.message}"

@dataclass
class Node:
    type_full: str
    code: str
    state: str | None
    props: list[str]
    region: str
    line_num: int
    is_first_mention: bool = True

@dataclass
class Edge:
    source: str  # code@region
    edge_type: str
    target: str  # code@region
    line_num: int

@dataclass
class ChainTerminal:
    fate_type: str  # one of VALID_FATES prefix
    detail: str | None
    line_num: int

@dataclass
class LintResult:
    issues: list[Issue] = field(default_factory=list)
    nodes: dict = field(default_factory=dict)  # code@region -> Node
    edges: list = field(default_factory=list)
    chains: list = field(default_factory=list)
    integrations: list = field(default_factory=list)
    protocols: list = field(default_factory=list)
    conditionals: list = field(default_factory=list)
    composites: list = field(default_factory=list)
    dysregs: list = field(default_factory=list)
    observables: list = field(default_factory=list)
    declared_fates: set = field(default_factory=set)
    declared_domains: set = field(default_factory=set)

    def error(self, check: str, line: int, msg: str):
        self.issues.append(Issue(Severity.ERROR, check, line, msg))

    def warn(self, check: str, line: int, msg: str):
        self.issues.append(Issue(Severity.WARN, check, line, msg))

    @property
    def errors(self):
        return [i for i in self.issues if i.severity == Severity.ERROR]

    @property
    def warnings(self):
        return [i for i in self.issues if i.severity == Severity.WARN]

    @property
    def valid(self):
        return len(self.errors) == 0


# ── PARSING HELPERS ──

NODE_RE = re.compile(
    r'\{(?:([A-Za-z0-9._-]+):)?'   # optional TYPE:
    r'([^\[\]@{},\s()]+)'          # CODE (anything except delimiters and parens)
    r'(?:\[([^\]]*)\])?'            # optional [STATE]
    r'(?:\(([^)]*)\))?'             # optional (PROPS)
    r'@([A-Za-z0-9_]+)\}'          # @REGION
)

EDGE_RE = re.compile(r'(→\?|→|⊣|~>|=>|\|>)')

FATE_RE = re.compile(r'(↺[⁺⁻⁰]|→⊘|→□|→≋|→Δm)')

INTEGRATE_RE = re.compile(
    r'∫\{([^}]+)\}←\(([^)]+)\)→([^:]+):(\w+)'
)

PROTOCOL_RE = re.compile(
    r'\{([^}]+)\}⊲\{([^}]+)\}\[([^\]]+)\]'
)

CONDITIONAL_RE = re.compile(
    r'⊗\(([^)]+)\)⟹(.+)'
)

COMPOSITE_RE = re.compile(
    r'◈(\w+)=(.+)'
)

DYSREG_RE = re.compile(
    r'⚡(\w+):(.+)\(([^)]+)\)'
)

OBSERVABLE_RE = re.compile(
    r'⊕\s*(\S+)\s*→\s*(.+)\s*\((\w+)\)'
)

MONITOR_RE = re.compile(
    r'⊕⊳\s*(\S+)\s*→\s*(.+)\s*\(([^)]+)\)'
)

DELTA_RE = re.compile(
    r'Δ\(([^)]+)\)=(\+\+|\+|=|~|-|--)'
)


def parse_node_refs(text: str) -> list[tuple[str, str]]:
    """Extract all {CODE@REGION} references from text."""
    return [(m.group(2), m.group(5)) for m in NODE_RE.finditer(text)]


def node_key(code: str, region: str) -> str:
    return f"{code}@{region}"


# ── MAIN LINTER ──

def lint(output: str) -> LintResult:
    result = LintResult()
    lines = output.strip().split("\n")
    seen_sections = []
    section_order = [
        "@domain", "#", "::fates", "::open_ends", "Δ(",
        "⊙", "chain", "↺⁰", "fate", "∫", "⊲", "⊗", "◈", "⚡", "⊕"
    ]

    for i, line in enumerate(lines, 1):
        line = line.strip()
        if not line:
            continue

        # ── HEADER ──
        if line.startswith("@domain:"):
            _check_domain(line, i, result)
            continue

        if line.startswith("#"):
            continue  # context tag

        if line.startswith("::fates"):
            _check_fates_decl(line, i, result)
            continue

        if line.startswith("::open_ends"):
            if "0" not in line:
                result.error("OPEN_ENDS", i, "::open_ends must be 0")
            continue

        # ── DELTA ──
        if line.startswith("Δ("):
            _check_delta(line, i, result)
            continue

        # ── CHAINS (including root ⊙) ──
        if line.startswith("⊙") or NODE_RE.search(line):
            if not any(c in line for c in ["∫", "⊲", "⊗", "◈", "⚡", "⊕"]):
                _check_chain(line, i, result)
                continue

        # ── INTEGRATION ──
        if line.startswith("∫"):
            _check_integration(line, i, result)
            continue

        # ── PROTOCOL ──
        if "⊲" in line and not line.startswith("⚡"):
            _check_protocol(line, i, result)
            continue

        # ── CONDITIONAL ──
        if line.startswith("⊗"):
            _check_conditional(line, i, result)
            continue

        # ── COMPOSITE ──
        if line.startswith("◈"):
            _check_composite(line, i, result)
            continue

        # ── DYSREG ──
        if line.startswith("⚡"):
            _check_dysreg(line, i, result)
            continue

        # ── OBSERVABLE ──
        if line.startswith("⊕"):
            _check_observable(line, i, result)
            continue

    # ── POST-PARSE CHECKS ──
    _check_cross_references(result)
    _check_fate_coverage(result)
    _check_pool_integrity(result)
    _check_behavioral_closure(result)
    _check_first_mention_typing(result)
    _check_bare_ligands(result)
    _check_receptor_region(result)

    return result


# ── SECTION CHECKERS ──

def _check_domain(line: str, num: int, r: LintResult):
    domains = line.replace("@domain:", "").strip().split(",")
    valid_domains = {"chem", "elec", "meta", "struct"}
    for d in domains:
        d = d.strip()
        if d and d not in valid_domains:
            r.error("DOMAIN", num, f"Unknown domain: {d}")
        if d:
            r.declared_domains.add(d.strip())


def _check_fates_decl(line: str, num: int, r: LintResult):
    fates_str = line.replace("::fates", "").strip()
    for f in fates_str.split(","):
        f = f.strip()
        if f:
            r.declared_fates.add(f)


def _check_delta(line: str, num: int, r: LintResult):
    m = DELTA_RE.search(line)
    if not m:
        r.error("DELTA", num, f"Malformed Δ declaration: {line}")
        return
    ref = m.group(1)
    if "@" not in ref:
        r.error("DELTA", num, f"Δ reference missing @REGION: {ref}")


def _check_chain(line: str, num: int, r: LintResult):
    """Parse and validate a chain line."""
    nodes_in_chain = []
    edges_in_chain = []

    for m in NODE_RE.finditer(line):
        type_full = m.group(1) or ""
        code = m.group(2)
        state_raw = m.group(3)
        props_raw = m.group(4)
        region = m.group(5)

        state = state_raw.split(",")[0].split(" ")[0] if state_raw else None
        props = [p.strip() for p in props_raw.split(",")] if props_raw else []

        # Validate region
        if region not in VALID_REGIONS:
            r.error("REGION", num, f"Unknown region '{region}' on {code}")

        # Validate state
        if state and state not in VALID_STATES:
            # Could be a barrier state like "leaky" or "tight"
            if state not in ("leaky", "tight"):
                r.warn("STATE", num, f"Non-standard state '{state}' on {code}@{region}")

        # Validate coupling props
        for p in props:
            if p in VALID_COUPLINGS:
                continue
            if ":" in p or p in ("up", "down", "des", "act", "block", "intern"):
                continue
            # Allow compound heteromer names, etc.

        key = node_key(code, region)
        node = Node(type_full, code, state, props, region, num)

        if key in r.nodes:
            node.is_first_mention = False
        else:
            r.nodes[key] = node

        nodes_in_chain.append(key)

    # Extract edges
    parts = EDGE_RE.split(line)
    # edges are at odd indices in split
    for i in range(1, len(parts), 2):
        edge_type = parts[i]
        if edge_type == "→?":
            edge_type = "→?"
        if i > 0 and i + 1 < len(parts):
            src_nodes = parse_node_refs(parts[i - 1])
            tgt_nodes = parse_node_refs(parts[i + 1])
            if src_nodes and tgt_nodes:
                src = node_key(src_nodes[-1][0], src_nodes[-1][1])
                tgt = node_key(tgt_nodes[0][0], tgt_nodes[0][1])
                edge = Edge(src, edge_type, tgt, num)
                r.edges.append(edge)
                edges_in_chain.append(edge)

    # Check fate termination
    has_fate = bool(FATE_RE.search(line))
    has_beh_pass = "→Δ(" in line and "↺" in line
    if not has_fate and not has_beh_pass:
        if nodes_in_chain:
            # Store for post-parse check — may be fan-out source
            r._pending_fate_check = getattr(r, '_pending_fate_check', [])
            r._pending_fate_check.append((nodes_in_chain[-1], num))

    # Check cascade compliance for intracellular chains
    _check_cascade_compliance(nodes_in_chain, edges_in_chain, num, r)

    r.chains.append({"nodes": nodes_in_chain, "edges": edges_in_chain, "line": num})


def _check_cascade_compliance(nodes: list[str], edges: list[Edge], num: int, r: LintResult):
    """Check mandatory cascade rules."""
    if len(nodes) < 2:
        return

    for i, edge in enumerate(edges):
        src_key = edge.source
        tgt_key = edge.target
        src_node = r.nodes.get(src_key)
        tgt_node = r.nodes.get(tgt_key)

        if not src_node or not tgt_node:
            continue

        src_type = src_node.type_full
        tgt_type = tgt_node.type_full

        if not src_type or not tgt_type:
            continue

        # GPCR cascade: L→R must be followed by R→Gp→2m→K
        if src_type in LIGAND_PREFIXES and tgt_type == "R" and edge.edge_type == "→":
            coupling = None
            for p in tgt_node.props:
                if p in GPCR_COUPLINGS:
                    coupling = "GPCR"
                elif p in ION_COUPLINGS:
                    coupling = "ionotropic"
                elif p in ("RTK", "JAK-STAT"):
                    coupling = p

            if coupling == "GPCR":
                # Next should be Gp
                if i + 1 < len(edges):
                    next_tgt = r.nodes.get(edges[i + 1].target)
                    if next_tgt and next_tgt.type_full and next_tgt.type_full != "Gp":
                        r.error("CASCADE_GPCR", num,
                                f"GPCR cascade skip: {tgt_key} should go to Gp, got {next_tgt.type_full}:{next_tgt.code}")

        # R(GPCR)→K skip check (must go through Gp→2m first)
        if src_type == "R" and tgt_type == "K" and edge.edge_type == "→":
            src_couplings = r.nodes.get(src_key)
            if src_couplings:
                is_gpcr = any(p in GPCR_COUPLINGS for p in src_couplings.props)
                if is_gpcr:
                    r.error("CASCADE_GPCR", num,
                            f"GPCR R→K skip: {src_key}→{tgt_key}. Must go R→Gp→2m→K")

        # Steroid/nuclear cascade: L.h→NR→TF→G
        if src_type == "L.h" and tgt_type == "NR" and edge.edge_type == "→":
            if i + 1 < len(edges):
                next_tgt = r.nodes.get(edges[i + 1].target)
                if next_tgt and next_tgt.type_full and next_tgt.type_full != "TF":
                    r.error("CASCADE_NUCLEAR", num,
                            f"Nuclear cascade skip: NR should go to TF, got {next_tgt.type_full}:{next_tgt.code}")

        # E→L check (enzyme produces ligand)
        if src_type == "E" and tgt_type in LIGAND_PREFIXES and edge.edge_type == "→":
            pass  # Valid: enzyme produces ligand

        # Bare L→K skip check
        if src_type in LIGAND_PREFIXES and tgt_type == "K" and edge.edge_type == "→":
            r.error("CASCADE_SKIP", num,
                    f"Ligand directly activates kinase: {src_key}→{tgt_key}. Must go through R→Gp→2m→K")


def _check_integration(line: str, num: int, r: LintResult):
    m = INTEGRATE_RE.search(line)
    if not m:
        r.error("INTEGRATE_SYNTAX", num, f"Malformed ∫: {line[:80]}")
        return

    unit = m.group(1)
    inputs_str = m.group(2)
    output = m.group(3).strip()
    mode = m.group(4)

    if mode not in VALID_INTEGRATE_MODES:
        r.error("INTEGRATE_MODE", num, f"Invalid mode '{mode}'. Valid: {VALID_INTEGRATE_MODES}")

    # Parse inputs
    for inp in inputs_str.split(","):
        inp = inp.strip()
        if not inp:
            continue
        parts = inp.rsplit(":", 1)
        if len(parts) != 2:
            r.error("INTEGRATE_INPUT", num, f"Input missing sign: {inp}")
            continue
        ref, sign = parts[0].strip(), parts[1].strip()
        if sign not in VALID_INTEGRATE_SIGNS:
            r.error("INTEGRATE_SIGN", num, f"Invalid sign '{sign}' for input {ref}")
        if "@" not in ref:
            r.error("INTEGRATE_REF", num, f"Input missing @REGION: {ref}")

    r.integrations.append({"unit": unit, "inputs": inputs_str, "output": output, "mode": mode, "line": num})


def _check_protocol(line: str, num: int, r: LintResult):
    m = PROTOCOL_RE.search(line)
    if not m:
        r.warn("PROTOCOL_SYNTAX", num, f"Could not parse ⊲: {line[:80]}")
        return

    source = m.group(1)
    target = m.group(2)
    terms = m.group(3)

    # Check terms are valid
    for term in terms.split():
        term_clean = term.strip()
        if term_clean.startswith("{") or term_clean.startswith("×"):
            continue  # gate condition or gain
        if term_clean not in VALID_PROTOCOL_TERMS:
            r.warn("PROTOCOL_TERM", num, f"Unknown protocol term: {term_clean}")

    r.protocols.append({"source": source, "target": target, "terms": terms, "line": num})


def _check_conditional(line: str, num: int, r: LintResult):
    m = CONDITIONAL_RE.search(line)
    if not m:
        r.error("CONDITIONAL_SYNTAX", num, f"Malformed ⊗: {line[:80]}")
        return

    condition = m.group(1)
    effect = m.group(2)

    # Check that condition references have >=
    if ">=" not in condition:
        r.warn("CONDITIONAL_COND", num, f"Condition missing >= operator: {condition}")

    # Check effect
    valid_effects = {"pass", "block", "amplify", "apoptosis"}
    has_valid = any(e in effect for e in valid_effects) or "switch:" in effect
    if not has_valid:
        r.error("CONDITIONAL_EFFECT", num, f"Unknown effect in: {effect}")

    r.conditionals.append({"condition": condition, "effect": effect, "line": num})


def _check_composite(line: str, num: int, r: LintResult):
    m = COMPOSITE_RE.search(line)
    if not m:
        r.error("COMPOSITE_SYNTAX", num, f"Malformed ◈: {line[:80]}")
        return

    name = m.group(1)
    refs = m.group(2)

    # Check references have @REGION
    for ref in refs.split("+"):
        ref = ref.strip().strip("{}")
        if ref and "@" not in ref:
            r.error("COMPOSITE_REF", num, f"Composite ref missing @REGION: {ref}")

    r.composites.append({"name": name, "refs": refs, "line": num})


def _check_dysreg(line: str, num: int, r: LintResult):
    m = DYSREG_RE.search(line)
    if not m:
        # Could be a flag without dynamics parens
        if "⚡" in line:
            dtype = line.split(":")[0].replace("⚡", "").strip() if ":" in line else ""
            if dtype and dtype not in VALID_DYSREG_TYPES:
                r.warn("DYSREG_TYPE", num, f"Unknown dysreg type: {dtype}")
            r.dysregs.append({"type": dtype, "line": num})
            return
        r.warn("DYSREG_SYNTAX", num, f"Could not parse ⚡: {line[:80]}")
        return

    dtype = m.group(1)
    chain = m.group(2)
    dynamics = m.group(3)

    if dtype not in VALID_DYSREG_TYPES:
        r.warn("DYSREG_TYPE", num, f"Unknown dysreg type: {dtype}")

    r.dysregs.append({"type": dtype, "chain": chain, "dynamics": dynamics, "line": num})


def _check_observable(line: str, num: int, r: LintResult):
    # Could be ⊕ or ⊕⊳
    m = OBSERVABLE_RE.search(line) or MONITOR_RE.search(line)
    if not m:
        r.warn("OBSERVABLE_SYNTAX", num, f"Could not parse ⊕: {line[:80]}")
        return

    measurement = m.group(1)
    refs = m.group(2)
    relationship = m.group(3)

    if relationship not in VALID_OBSERVABLE_RELS and not line.startswith("⊕⊳"):
        r.warn("OBSERVABLE_REL", num, f"Unknown relationship: {relationship}")

    r.observables.append({"measurement": measurement, "refs": refs, "line": num})


# ── POST-PARSE CHECKS ──

def _check_cross_references(r: LintResult):
    """Check that ∫ inputs, ⊲ targets, and ⊗ conditions reference existing nodes."""
    for integ in r.integrations:
        for inp in integ["inputs"].split(","):
            inp = inp.strip()
            if not inp:
                continue
            ref = inp.rsplit(":", 1)[0].strip()
            if "@" in ref:
                code, region = ref.rsplit("@", 1)
                key = node_key(code.strip(), region.strip())
                if key not in r.nodes:
                    r.error("PHANTOM_REF", integ["line"],
                            f"∫ input {key} not found in any chain")

    for cond in r.conditionals:
        for ref_match in NODE_RE.finditer(cond["condition"]):
            code, region = ref_match.group(2), ref_match.group(5)
            key = node_key(code, region)
            if key not in r.nodes:
                r.error("PHANTOM_REF", cond["line"],
                        f"⊗ condition references {key} not found in chains")


def _check_fate_coverage(r: LintResult):
    """Check chains without fates — allow if last node is source of another chain."""
    pending = getattr(r, '_pending_fate_check', [])
    chain_sources = set()
    for chain in r.chains:
        if chain["nodes"]:
            chain_sources.add(chain["nodes"][0])
    # Also count edge sources
    edge_sources = {e.source for e in r.edges}

    for last_node, line_num in pending:
        if last_node in chain_sources or last_node in edge_sources:
            pass  # Fan-out node — other chains continue from here
        else:
            r.error("FATE", line_num,
                    f"Chain ends at {last_node} with no fate and no fan-out")


def _check_pool_integrity(r: LintResult):
    """Check that recycling loops have V: pool nodes and ∫ includes pools as ×."""
    pool_nodes = {k: v for k, v in r.nodes.items() if v.type_full == "V"}
    recycling_transmitters = set()

    for chain in r.chains:
        for nk in chain["nodes"]:
            node = r.nodes.get(nk)
            if node and node.type_full == "T":
                # Transport node — find what it's transporting
                recycling_transmitters.add(nk.split("@")[0].replace("DAT", "DA").replace("SERT", "5HT").replace("NET", "NE"))

    # Check that transmitters with transporters have V: pool nodes
    for nt in recycling_transmitters:
        has_pool = any(k.startswith(f"ves_{nt}") or k.startswith(f"ves_") and nt in k for k in pool_nodes)
        if not has_pool:
            r.warn("POOL_MISSING", 0,
                    f"Transporter for {nt} found but no V:ves_{nt} pool node in recycling loop")


def _check_behavioral_closure(r: LintResult):
    """Check that every B.beh node has both input and output edges."""
    beh_nodes = {k for k, v in r.nodes.items() if v.type_full == "B.beh"}

    for beh in beh_nodes:
        has_input = any(e.target == beh for e in r.edges)
        has_output = any(e.source == beh for e in r.edges)

        # Check chain position — if B.beh is not the last node, it has output
        # Also check for →Δ pass-through (B.beh followed by →Δ in chain)
        if not has_output:
            for chain in r.chains:
                if beh in chain["nodes"]:
                    idx = chain["nodes"].index(beh)
                    if idx < len(chain["nodes"]) - 1:
                        has_output = True  # Not last node = continues
                        break
                    # Last node but chain has a beh_passthrough fate
                    # (detected by ↺ after →Δ in the original line)
                    # Check if any edge sources from this B.beh exist
                    # via the pass-through notation
                    if any(beh in chain["nodes"] for chain in r.chains
                           if chain["nodes"] and chain["nodes"][-1] != beh):
                        has_output = True
                        break

        if not has_input:
            r.error("BEH_NO_INPUT", r.nodes[beh].line_num,
                    f"B.beh node {beh} has no input edge from circuit")
        if not has_output:
            r.error("BEH_NO_OUTPUT", r.nodes[beh].line_num,
                    f"B.beh node {beh} has no output edge back to circuit")


def _check_first_mention_typing(r: LintResult):
    """Check that first mention of every node has full type declaration."""
    seen = set()
    for chain in r.chains:
        for nk in chain["nodes"]:
            node = r.nodes.get(nk)
            if not node:
                continue
            if nk not in seen:
                seen.add(nk)
                if not node.type_full:
                    r.error("FIRST_MENTION", node.line_num,
                            f"First mention of {nk} missing type declaration")


def _check_bare_ligands(r: LintResult):
    """Check that no ligand uses bare L: without subclass."""
    for key, node in r.nodes.items():
        if node.type_full == "L":
            r.error("BARE_LIGAND", node.line_num,
                    f"Bare L: on {key}. Must use subclass: L.nt, L.h, L.p, L.cb, L.ni, L.ns, L.mb")


# Typical receptor expression by region (soft check — warns, not errors)
TYPICAL_RECEPTOR_REGIONS = {
    "D1": {"NAc", "striatum", "PFC", "ACC", "AMY", "HPC"},
    "D2": {"NAc", "striatum", "VTA", "PFC", "PIT", "ARC"},
    "D3": {"NAc", "VTA"},
    "D4": {"PFC", "HPC", "striatum"},
    "D5": {"HPC", "PFC", "NAc"},
    "5HT1A": {"DRN", "HPC", "PFC", "AMY", "ACC"},
    "5HT1B": {"DRN", "NAc", "striatum", "VTA"},
    "5HT2A": {"PFC", "ACC", "AMY", "HPC", "INS", "striatum"},
    "5HT2C": {"VTA", "NAc", "PFC", "ARC"},
    "5HT3": {"NTS", "AMY", "HPC", "ENS"},
    "5HT4": {"ENS", "HPC", "striatum"},
    "α1": {"PFC", "AMY", "LC", "thalamus"},
    "α2A": {"PFC", "LC", "AMY", "HPC", "NTS"},
    "β1": {"cardiac", "PFC", "HPC"},
    "β2": {"cardiac", "PFC", "HPC", "AMY"},
    "GABA-A": None,  # ubiquitous
    "GABA-B": None,  # ubiquitous
    "NMDA": None,    # ubiquitous
    "AMPA": None,    # ubiquitous
    "GR": None,      # ubiquitous
    "MR": {"HPC", "AMY", "PFC", "PVN", "kidney", "cardiac"},
    "MOR": {"VTA", "PAG", "RVM", "NAc", "AMY", "spinal", "DRN", "LC"},
    "KOR": {"NAc", "VTA", "DRN", "AMY", "HPC"},
    "DOR": {"AMY", "HPC", "PFC", "striatum"},
    "CB1": {"HPC", "PFC", "NAc", "AMY", "striatum", "PAG", "ACC"},
    "CB2": {"CNS", "systemic"},  # mainly immune cells, some CNS
    "TrkB": None,    # ubiquitous in CNS
    "NK1": {"spinal", "AMY", "PAG", "DRN", "NTS"},
    "OXTR": {"AMY", "PVN", "NAc", "VTA", "PFC", "HPC"},
    "A1": {"HPC", "PFC", "thalamus", "DRN", "striatum"},
    "A2A": {"striatum", "NAc", "GPe", "VTA"},
    "H1": {"thalamus", "PFC", "HPC", "AMY"},
    "H3": {"striatum", "NAc", "PFC", "HPC", "thalamus"},
    "GlyR": {"spinal", "pons", "DRN"},
    "nAChR": {"VTA", "PFC", "HPC", "NAc", "pons", "thalamus"},
    "M1": {"HPC", "PFC", "striatum", "AMY"},
    "mAChR": {"HPC", "PFC", "striatum", "AMY"},
    "GHSR": {"ARC", "VTA", "HPC"},
    "LepR": {"ARC", "VTA", "HPC", "PVN"},
    "InsR": {"ARC", "HPC", "VTA", "PFC"},
    "TSHR": {"THYROID"},
    "GnRH-R": {"PIT"},
    "LHR": {"GONAD"},
    "FSHR": {"GONAD"},
    "MC2R": {"ADR"},
    "PRLR": {"PIT", "HPC", "PVN"},
    "TLR4": {"CNS", "systemic", "GUT", "ENS"},
    "IL6R": {"CNS", "systemic", "HPC", "PFC"},
}


def _check_receptor_region(r: LintResult):
    """Soft check: flag receptors at atypical regions."""
    for key, node in r.nodes.items():
        if node.type_full == "R" and node.code in TYPICAL_RECEPTOR_REGIONS:
            typical = TYPICAL_RECEPTOR_REGIONS[node.code]
            if typical is not None and node.region not in typical:
                r.warn("ATYPICAL_REGION", node.line_num,
                       f"{node.code}@{node.region} is atypical. "
                       f"Usually: {', '.join(sorted(typical))}")


# ── CLI ──

def format_report(result: LintResult, partial: bool = False) -> str:
    lines = []
    lines.append(f"BioChain Linter v2.5{' (partial)' if partial else ''}")
    lines.append(f"{'='*50}")
    lines.append(f"Nodes:        {len(result.nodes)}")
    lines.append(f"Edges:        {len(result.edges)}")
    lines.append(f"Chains:       {len(result.chains)}")
    lines.append(f"Integrations: {len(result.integrations)}")
    lines.append(f"Protocols:    {len(result.protocols)}")
    lines.append(f"Conditionals: {len(result.conditionals)}")
    lines.append(f"Composites:   {len(result.composites)}")
    lines.append(f"Dysregs:      {len(result.dysregs)}")
    lines.append(f"Observables:  {len(result.observables)}")
    lines.append(f"{'='*50}")
    lines.append(f"Errors:   {len(result.errors)}")
    lines.append(f"Warnings: {len(result.warnings)}")
    lines.append(f"Valid:    {'YES' if result.valid else 'NO'}")
    lines.append("")

    if result.errors:
        lines.append("ERRORS:")
        for issue in result.errors:
            lines.append(f"  {issue}")
        lines.append("")

    if result.warnings:
        lines.append("WARNINGS:")
        for issue in result.warnings:
            lines.append(f"  {issue}")

    return "\n".join(lines)


def lint_partial(output: str) -> LintResult:
    """Lint partial/incomplete BioChain output.

    Runs all per-line checks (cascade, types, regions, bare ligands)
    but skips checks that require the full document:
    - Cross-reference validation (∫ inputs exist in chains)
    - Fate coverage (every chain terminates)
    - Pool integrity (transporters have pools)
    - Behavioral closure (B.beh has input AND output)

    Use during generation: after each section, run lint_partial()
    to catch errors before they compound downstream.

    Usage:
        # After generating chains:
        result = lint_partial(chains_so_far)
        if result.errors:
            # Fix now — don't generate ∫ on broken chains

        # After adding ∫:
        result = lint_partial(chains_plus_integrations)
        # Now checks ∫ syntax + cross-refs against known chains

        # After full output:
        result = lint(full_output)  # full checks including closure
    """
    result = LintResult()
    lines = output.strip().split("\n")

    has_chains = False
    has_integrations = False

    for i, line in enumerate(lines, 1):
        line = line.strip()
        if not line:
            continue

        # ── HEADER ──
        if line.startswith("@domain:"):
            _check_domain(line, i, result)
            continue
        if line.startswith("#"):
            continue
        if line.startswith("::fates"):
            _check_fates_decl(line, i, result)
            continue
        if line.startswith("::open_ends"):
            if "0" not in line:
                result.error("OPEN_ENDS", i, "::open_ends must be 0")
            continue

        # ── DELTA ──
        if line.startswith("Δ("):
            _check_delta(line, i, result)
            continue

        # ── CHAINS ──
        if line.startswith("⊙") or NODE_RE.search(line):
            if not any(c in line for c in ["∫", "⊲", "⊗", "◈", "⚡", "⊕"]):
                _check_chain(line, i, result)
                has_chains = True
                continue

        # ── INTEGRATION ──
        if line.startswith("∫"):
            _check_integration(line, i, result)
            has_integrations = True
            continue

        # ── PROTOCOL ──
        if "⊲" in line and not line.startswith("⚡"):
            _check_protocol(line, i, result)
            continue

        # ── CONDITIONAL ──
        if line.startswith("⊗"):
            _check_conditional(line, i, result)
            continue

        # ── COMPOSITE ──
        if line.startswith("◈"):
            _check_composite(line, i, result)
            continue

        # ── DYSREG ──
        if line.startswith("⚡"):
            _check_dysreg(line, i, result)
            continue

        # ── OBSERVABLE ──
        if line.startswith("⊕"):
            _check_observable(line, i, result)
            continue

    # ── PARTIAL POST-PARSE ──
    # Always check: first-mention typing, bare ligands, receptor regions
    _check_first_mention_typing(result)
    _check_bare_ligands(result)
    _check_receptor_region(result)

    # Only check cross-refs if we have integrations (they ref chains)
    if has_integrations and has_chains:
        _check_cross_references(result)

    # Skip: fate coverage, pool integrity, behavioral closure
    # These require complete output to be meaningful

    return result


def main():
    if len(sys.argv) < 2:
        print("Usage: python biochain_lint.py <file.bc>")
        print("       python biochain_lint.py --partial <file.bc>")
        print("       cat output.txt | python biochain_lint.py -")
        print("       cat partial.txt | python biochain_lint.py --partial -")
        sys.exit(1)

    partial = "--partial" in sys.argv
    args = [a for a in sys.argv[1:] if a != "--partial"]

    if not args:
        print("Error: no input file specified")
        sys.exit(1)

    path = args[0]
    if path == "-":
        text = sys.stdin.read()
    else:
        with open(path) as f:
            text = f.read()

    if partial:
        result = lint_partial(text)
    else:
        result = lint(text)

    print(format_report(result, partial=partial))
    sys.exit(0 if result.valid else 1)


if __name__ == "__main__":
    main()
