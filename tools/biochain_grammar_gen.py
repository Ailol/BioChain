#!/usr/bin/env python3
"""Generate an EBNF grammar for BioChain with the knowledge graph
unrolled into production rules.

Instead of a generic receptor rule + post-hoc validation, each ligand
gets its own rule that only allows biologically valid receptors with
correct coupling. The grammar stays context-free but encodes semantics.

Output: EBNF compatible with xgrammar, outlines, SGLang, lm-format-enforcer.
"""

# ═══════════════════════════════════════
# KNOWLEDGE GRAPH (source of truth)
# ═══════════════════════════════════════

# Ligand → [(receptor, coupling), ...]
# coupling determines cascade shape downstream

BINDINGS = {
    # L.nt neurotransmitters
    "DA":   [("D1","Gs"), ("D2","Gi"), ("D3","Gi"), ("D4","Gi"), ("D5","Gs"),
             ("TAAR1","Gs")],
    "5HT":  [("5HT1A","Gi"), ("5HT1B","Gi"), ("5HT2A","Gq"), ("5HT2B","Gq"),
             ("5HT2C","Gq"), ("5HT3","Na⁺"), ("5HT4","Gs"), ("5HT6","Gs"), ("5HT7","Gs")],
    "NE":   [("α1","Gq"), ("α2A","Gi"), ("α2B","Gi"), ("α2C","Gi"),
             ("β1","Gs"), ("β2","Gs"), ("β3","Gs")],
    "GABA": [("GABA-A","Cl⁻"), ("GABA-B","Gi")],
    "GLU":  [("NMDA","Ca²⁺"), ("AMPA","Na⁺"), ("kainate","Na⁺"),
             ("mGluR1","Gq"), ("mGluR2","Gi"), ("mGluR3","Gi"),
             ("mGluR4","Gi"), ("mGluR5","Gq")],
    "ACh":  [("nAChR","Na⁺"), ("mAChR","Gq"), ("M1","Gq"), ("M2","Gi"),
             ("M3","Gq"), ("M4","Gi"), ("M5","Gq")],
    "adenosine": [("A1","Gi"), ("A2A","Gs"), ("A2B","Gs"), ("A3","Gi")],
    "histamine": [("H1","Gq"), ("H2","Gs"), ("H3","Gi"), ("H4","Gi")],
    "glycine": [("GlyR","Cl⁻"), ("NMDA","Ca²⁺")],
    "D-serine": [("NMDA","Ca²⁺")],
    "ATP":  [("P2X","Ca²⁺"), ("P2Y","Gq")],

    # L.h hormones — GPCR
    "CRH":       [("CRH-R1","Gs"), ("CRH-R2","Gs")],
    "TRH":       [("TRH-R","Gq")],
    "ACTH":      [("MC2R","Gs")],
    "melatonin": [("MT1","Gi"), ("MT2","Gi")],
    "ghrelin":   [("GHSR","Gq")],
    "GLP-1":     [("GLP1R","Gs")],
    "CCK":       [("CCK-A","Gq"), ("CCK-B","Gq")],
    "PYY":       [("Y2R","Gi"), ("Y4R","Gi")],
    "TSH":       [("TSHR","Gs")],
    "GnRH":      [("GnRH-R","Gq")],
    "LH":        [("LHR","Gs")],
    "FSH":       [("FSHR","Gs")],
    "PGE2":      [("EP1","Gq"), ("EP2","Gs"), ("EP3","Gi"), ("EP4","Gs")],

    # L.h hormones — nuclear
    "CORT":        [("GR","nuclear"), ("MR","nuclear")],
    "estradiol":   [("ERα","nuclear"), ("ERβ","nuclear")],
    "testosterone": [("AR","nuclear")],
    "progesterone": [("PR","nuclear")],
    "aldosterone":  [("MR","nuclear")],
    "T3":          [("TRα","nuclear"), ("TRβ","nuclear")],
    "T4":          [("TRα","nuclear"), ("TRβ","nuclear")],

    # L.h hormones — RTK / JAK-STAT
    "insulin":   [("InsR","RTK")],
    "leptin":    [("LepR","JAK-STAT")],
    "prolactin": [("PRLR","JAK-STAT")],

    # L.p peptides
    "BDNF":        [("TrkB","RTK"), ("p75NTR","RTK")],
    "NGF":         [("TrkA","RTK"), ("p75NTR","RTK")],
    "OXT":         [("OXTR","Gq")],
    "NPY":         [("Y1R","Gi"), ("Y2R","Gi"), ("Y5R","Gi")],
    "dynorphin":   [("KOR","Gi")],
    "orexin":      [("OX1R","Gq"), ("OX2R","Gq")],
    "substance_P": [("NK1","Gq")],
    "VIP":         [("VPAC1","Gs"), ("VPAC2","Gs")],
    "CGRP":        [("CGRP-R","Gs"), ("CLR-RAMP1","Gs")],
    "β-endorphin": [("MOR","Gi"), ("DOR","Gi")],
    "motilin":     [("MLNR","Gq")],

    # L.cb endocannabinoids
    "2-AG":  [("CB1","Gi"), ("CB2","Gi")],
    "AEA":   [("CB1","Gi"), ("CB2","Gi"), ("TRPV1","Ca²⁺")],

    # L.ni neuroimmune
    "IL6":   [("IL6R","JAK-STAT")],
    "TNFα":  [("TNFR1","JAK-STAT"), ("TNFR2","JAK-STAT")],
    "IL1b":  [("IL1R1","JAK-STAT")],
    "IL10":  [("IL10R","JAK-STAT")],
    "IFNγ":  [("IFNGR","JAK-STAT")],
    "QUIN":  [("NMDA","Ca²⁺")],
    "LPS":   [("TLR4","JAK-STAT")],

    # L.ns neurosteroids (allosteric modulation — ~> not →)
    "allopregnanolone": [("GABA-A","Cl⁻")],
    "DHEAS":            [("GABA-A","Cl⁻"), ("NMDA","Ca²⁺")],

    # L.mb microbiome
    "butyrate":   [("FFAR2","Gi"), ("FFAR3","Gi"), ("GPR109A","Gi")],
    "propionate": [("FFAR2","Gi"), ("FFAR3","Gi")],
    "indole":     [("AhR","nuclear")],

    # Gasotransmitter (special — NO isn't a ligand in the classical sense)
    "NO":   [("sGC","cGMP")],
}

# Enzyme → product (or None for degradation)
ENZYME_PRODUCT = {
    "TH": "DA", "DDC": "DA", "DBH": "NE",
    "TPH1": "5HT", "TPH2": "5HT",
    "GAD67": "GABA", "GAD65": "GABA",
    "ChAT": "ACh",
    "IDO": "KYN",
    "5α-reductase": "allopregnanolone",
    "CYP19A1": "estradiol",
    "deiodinase_D2": "T3", "deiodinase_D1": "T3", "deiodinase_D3": "rT3",
    "GLS": "GLU", "GS": "GLN",
    "nNOS": "NO", "iNOS": "NO", "eNOS": "NO",
    "AANAT": "NAS",
    "ASMT": "melatonin",
    "BACE1": "amyloid-β",
    "γ-secretase": "amyloid-β",
}

# Degradation enzymes → clearance (→⊘)
DEGRADATION_ENZYMES = {"MAO-A", "MAO-B", "COMT", "CYP3A4", "MAGL", "FAAH",
                        "11β-HSD2", "ADH", "ALDH2", "COX1", "COX2",
                        "AChE", "BChE"}

# Transporter → substrates
TRANSPORTER_SUB = {
    "DAT": ["DA"], "SERT": ["5HT"], "NET": ["NE"],
    "VMAT2": ["DA", "5HT", "NE"],
    "LAT1": ["TRP"], "EAAT2": ["GLU"], "GAT1": ["GABA"], "SN1": ["GLN"],
}

# ── DEEP CASCADE CONSTRAINTS ──

# Coupling → which second messenger(s)
COUPLING_2M = {
    "Gs": ["cAMP"],
    "Gi": ["cAMP"],         # same molecule, but state will be [-] (inhibitory)
    "Gq": ["IP3", "DAG"],   # PLC pathway: both produced
    "G12": ["Rho"],
    "cGMP": ["cGMP"],       # NO→sGC→cGMP (sGC acts like Gp, cGMP is the 2m)
}

# Second messenger → which kinase(s)
SM_KINASE = {
    "cAMP":  ["PKA", "EPAC"],
    "IP3":   ["PKC"],           # via Ca²⁺ release from ER
    "DAG":   ["PKC"],
    "Ca²⁺":  ["CaMKII", "CaMKIV", "calcineurin", "PKC"],
    "Cl⁻":   [],                # ionotropic — no kinase step
    "Na⁺":   [],                # ionotropic
    "Rho":   ["ROCK"],
    "cGMP":  ["PKG"],           # NO→sGC→cGMP→PKG
}

# Kinase → valid downstream targets (kinases or TFs)
KINASE_TARGETS = {
    "PKA":         ["CREB", "DARPP-32", "GluA1", "TH", "TPH2"],
    "PKC":         ["MARCKS", "GAP43", "CREB", "NF-κB"],
    "CaMKII":      ["CREB", "GluA1", "TH", "TPH2", "nNOS"],
    "CaMKIV":      ["CREB"],
    "calcineurin":  ["NFAT"],
    "EPAC":        ["Rap1"],
    "ROCK":        ["LIMK", "MLC"],
    "PKG":         ["VASP", "CREB"],
    # RTK downstream
    "ERK":         ["CREB", "Elk-1"],
    "Akt":         ["mTOR", "GSK3β", "CREB", "BAD", "FOXO"],
    "PI3K":        ["Akt"],
    "PLCγ":        ["IP3", "DAG"],
    "JNK":         ["c-Jun", "AP-1"],
    # JAK-STAT downstream
    "JAK2":        ["STAT3", "STAT5", "STAT1"],
    "JAK1":        ["STAT3", "STAT1", "STAT5"],
    "TYK2":        ["STAT1", "STAT3"],
    # Other
    "GSK3β":       ["β-catenin", "tau"],
}

# Nuclear receptor → valid TF targets
NR_TF = {
    "GR":   ["NF-κB", "CREB", "AP-1"],
    "MR":   ["NF-κB", "SGK1", "CREB"],
    "ERα":  ["CREB", "AP-1", "Sp1", "NF-κB"],
    "ERβ":  ["CREB", "AP-1"],
    "TRα":  ["CREB"],
    "TRβ":  ["CREB", "NF-κB"],
    "PR":   ["CREB", "AP-1", "NF-κB"],
    "AR":   ["CREB", "AP-1"],
    "AhR":  ["ARNT", "NF-κB"],
}

# RTK → which kinases (receptor-specific)
RTK_KINASES = {
    "TrkB":    ["ERK", "Akt", "PLCγ"],
    "TrkA":    ["ERK", "Akt", "PLCγ"],
    "p75NTR":  ["JNK", "NF-κB"],
    "InsR":    ["PI3K", "Akt", "ERK"],
}

# JAK-STAT → which JAK kinase (receptor-specific)
JAKSTAT_KINASES = {
    "LepR":  ["JAK2"],
    "PRLR":  ["JAK2"],
    "IL6R":  ["JAK1", "JAK2"],
    "IL10R": ["JAK1", "TYK2"],
    "IFNGR": ["JAK1", "JAK2"],
    "TNFR1": ["JAK1"],
    "TNFR2": ["JAK1"],
    "IL1R1": ["JAK1"],
    "TLR4":  ["JAK1"],
}


# ═══════════════════════════════════════
# GRAMMAR GENERATOR
# ═══════════════════════════════════════

def safe_name(code: str) -> str:
    """Make a code safe for EBNF rule names."""
    return (code.replace("-", "_").replace(".", "_").replace("α", "alpha")
            .replace("β", "beta").replace("γ", "gamma").replace("⁺", "plus")
            .replace("⁻", "minus").replace("²", "2").replace("κ", "kappa"))


def generate_ebnf() -> str:
    rules = []
    indent = "    "

    # ── HEADER ──
    rules.append("# BioChain v2.5 — Knowledge-Graph-Constrained EBNF")
    rules.append("# Auto-generated. Do not edit manually.")
    rules.append("")

    # ── TOP LEVEL ──
    rules.append('document ::= header fates_decl open_ends delta* chain+ recycling* fate_chain* integration* protocol* conditional* composite* dysreg* observable*')
    rules.append('header ::= "@domain:" domain_list "\\n" context?')
    rules.append('domain_list ::= domain ("," domain)*')
    rules.append('domain ::= "chem" | "elec" | "meta" | "struct"')
    rules.append('context ::= "#" [^\\n]+ "\\n"')
    rules.append('fates_decl ::= "::fates " [^\\n]+ "\\n"')
    rules.append('open_ends ::= "::open_ends 0\\n"')
    rules.append('')

    # ── STATES ──
    rules.append('state ::= "[" state_val "]"')
    rules.append('state_val ::= "++" | "+" | "=" | "~" | "-" | "--" | "X" | "*"')
    rules.append('')

    # ── REGIONS ──
    all_regions = [
        "PVN", "LC", "DRN", "VTA", "NAc", "AMY", "BLA", "CeA", "HPC", "PFC",
        "ACC", "INS", "SCN", "PIT", "PAG", "RVM", "EC", "SN", "LH", "BST",
        "POA", "DG", "thalamus", "NBM", "CNS",
        "striatum", "GPi", "GPe", "STN",
        "pons", "SLD", "spinal",
        "ENS", "GUT", "VAG", "NTS", "DMV", "AP",
        "ARC", "ADR", "THYROID", "GONAD",
        "LIVER", "systemic", "plasma", "kidney", "cardiac",
        "behavior",
    ]
    rules.append('region ::= ' + ' | '.join(f'"{r}"' for r in all_regions))
    rules.append('')

    # ── EDGES ──
    rules.append('edge_act ::= "→"')
    rules.append('edge_inh ::= "⊣"')
    rules.append('edge_mod ::= "~>"')
    rules.append('edge_tx  ::= "=>"')
    rules.append('edge_tr  ::= "|>"')
    rules.append('')

    # ── FATES ──
    rules.append('fate ::= fate_loop | fate_clear | fate_seq | fate_diff | fate_sub')
    rules.append('fate_loop ::= "↺⁺" | "↺⁻" | "↺⁻(" [^)]+ ")" | "↺⁰" | "↺⁰(" [^)]+ ")"')
    rules.append('fate_clear ::= "→⊘"')
    rules.append('fate_seq ::= "→□(" [^)]+ ")"')
    rules.append('fate_diff ::= "→≋"')
    rules.append('fate_sub ::= "→Δm(" [^)]+ ")"')
    rules.append('')

    # ── DELTA ──
    rules.append('delta ::= "Δ(" code "@" region ")=" delta_sign exogenous? "\\n"')
    rules.append('delta_sign ::= "++" | "+" | "=" | "~" | "-" | "--"')
    rules.append('exogenous ::= "(exogenous:" [^)]+ ")"')
    rules.append('')

    # ── LIGAND → RECEPTOR CASCADES (the big unroll) ──
    rules.append("# ── LIGAND-RECEPTOR BINDING (knowledge graph encoded) ──")
    rules.append("")

    # Build per-ligand rules
    all_ligand_rules = []

    for ligand, receptors in BINDINGS.items():
        lig_safe = safe_name(ligand)

        # Determine ligand type
        if ligand in ("DA", "5HT", "NE", "GABA", "GLU", "ACh",
                       "adenosine", "histamine", "glycine", "D-serine", "ATP"):
            lig_type = "L.nt"
        elif ligand in ("CORT", "ACTH", "CRH", "TRH", "melatonin", "insulin",
                        "ghrelin", "GLP-1", "CCK", "PYY", "leptin", "T3", "T4",
                        "TSH", "estradiol", "testosterone", "progesterone",
                        "aldosterone", "prolactin", "LH", "FSH", "GnRH", "PGE2"):
            lig_type = "L.h"
        elif ligand in ("BDNF", "NGF", "OXT", "NPY", "dynorphin", "orexin",
                        "substance_P", "VIP", "CGRP", "β-endorphin", "motilin"):
            lig_type = "L.p"
        elif ligand in ("2-AG", "AEA"):
            lig_type = "L.cb"
        elif ligand in ("IL6", "TNFα", "IL1b", "IL10", "IFNγ", "QUIN", "LPS"):
            lig_type = "L.ni"
        elif ligand in ("allopregnanolone", "DHEAS"):
            lig_type = "L.ns"
        elif ligand in ("butyrate", "propionate", "indole"):
            lig_type = "L.mb"
        elif ligand == "NO":
            lig_type = "L.gas"  # gasotransmitter — special type
        else:
            lig_type = "L.h"

        # Node rule for this ligand
        rules.append(f'# {ligand}')
        rules.append(f'lig_{lig_safe}_node ::= "{{" "{lig_type}:{ligand}" state? "@" region "}}"')

        # Build receptor alternatives grouped by cascade type
        gpcr_alts = []
        ion_alts = []
        nuclear_alts = []
        rtk_alts = []
        jakstat_alts = []
        cgmp_alts = []

        for rec, coupling in receptors:
            rec_safe = safe_name(rec)
            if coupling in ("Gs", "Gi", "Gq", "G12"):
                gpcr_alts.append((rec, coupling, rec_safe))
            elif coupling in ("Cl⁻", "Ca²⁺", "Na⁺", "K⁺"):
                ion_alts.append((rec, coupling, rec_safe))
            elif coupling == "nuclear":
                nuclear_alts.append((rec, coupling, rec_safe))
            elif coupling == "RTK":
                rtk_alts.append((rec, coupling, rec_safe))
            elif coupling == "JAK-STAT":
                jakstat_alts.append((rec, coupling, rec_safe))
            elif coupling == "cGMP":
                cgmp_alts.append((rec, coupling, rec_safe))

        # Edge type depends on L.ns (modulation) vs others (activation)
        edge = '"~>"' if lig_type == "L.ns" else '"→"'

        receptor_alts = []

        # GPCR: L→R(G)→Gp→2m→K  (fully constrained)
        for rec, coupling, rec_safe in gpcr_alts:
            valid_2ms = COUPLING_2M.get(coupling, [])
            sub_alts = []
            for sm in valid_2ms:
                sm_safe = safe_name(sm)
                valid_kinases = SM_KINASE.get(sm, [])
                if valid_kinases:
                    k_options = ' | '.join(f'"{k}"' for k in valid_kinases)
                    sub_name = f'lig_{lig_safe}_rec_{rec_safe}_{sm_safe}'
                    rules.append(
                        f'{sub_name} ::= {edge} "{{R:{rec}({coupling})@" region "}}"'
                        f' "→{{Gp:{coupling}@" region "}}"'
                        f' "→{{2m:{sm}" state? "@" region "}}"'
                        f' "→{{K:" ({k_options}) state? "@" region "}}"'
                    )
                    sub_alts.append(sub_name)
                else:
                    # No kinase step (shouldn't happen for GPCR but safety)
                    sub_name = f'lig_{lig_safe}_rec_{rec_safe}_{sm_safe}'
                    rules.append(
                        f'{sub_name} ::= {edge} "{{R:{rec}({coupling})@" region "}}"'
                        f' "→{{Gp:{coupling}@" region "}}"'
                        f' "→{{2m:{sm}" state? "@" region "}}"'
                    )
                    sub_alts.append(sub_name)

            rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
            if sub_alts:
                rules.append(f'{rule_name} ::= ' + ' | '.join(sub_alts))
            else:
                rules.append(f'{rule_name} ::= {edge} "{{R:{rec}({coupling})@" region "}}"'
                             f' "→{{Gp:{coupling}@" region "}}"'
                             f' "→{{2m:cAMP" state? "@" region "}}"')
            receptor_alts.append(rule_name)

        # Ionotropic: L→R(ion)→2m
        for rec, coupling, rec_safe in ion_alts:
            rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
            rules.append(f'{rule_name} ::= {edge} "{{R:{rec}({coupling})@" region "}}" "→{{2m:{coupling}" state? "@" region "}}"')
            receptor_alts.append(rule_name)

        # Nuclear: L→NR→TF→G  (TF constrained by NR)
        for rec, coupling, rec_safe in nuclear_alts:
            valid_tfs = NR_TF.get(rec, [])
            if valid_tfs:
                tf_options = ' | '.join(f'"{tf}"' for tf in valid_tfs)
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(
                    f'{rule_name} ::= "→{{NR:{rec}@" region "}}"'
                    f' "→{{TF:" ({tf_options}) state? "@" region "}}"'
                    f' "=>" rest_of_chain'
                )
            else:
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(
                    f'{rule_name} ::= "→{{NR:{rec}@" region "}}"'
                    f' "→{{TF:" code state? "@" region "}}"'
                    f' "=>" rest_of_chain'
                )
            receptor_alts.append(rule_name)

        # RTK: L→R(RTK)→K→TF  (K constrained by receptor)
        for rec, coupling, rec_safe in rtk_alts:
            valid_kinases = RTK_KINASES.get(rec, [])
            if valid_kinases:
                # Build K→TF chains for each valid kinase
                k_sub_alts = []
                for k in valid_kinases:
                    k_safe = safe_name(k)
                    valid_tfs = KINASE_TARGETS.get(k, [])
                    if valid_tfs:
                        tf_options = ' | '.join(f'"{tf}"' for tf in valid_tfs)
                        k_sub = f'lig_{lig_safe}_rec_{rec_safe}_k_{k_safe}'
                        rules.append(
                            f'{k_sub} ::= "→{{R:{rec}(RTK)@" region "}}"'
                            f' "→{{K:{k}" state? "@" region "}}"'
                            f' "→{{TF:" ({tf_options}) state? "@" region "}}"'
                        )
                    else:
                        k_sub = f'lig_{lig_safe}_rec_{rec_safe}_k_{k_safe}'
                        rules.append(
                            f'{k_sub} ::= "→{{R:{rec}(RTK)@" region "}}"'
                            f' "→{{K:{k}" state? "@" region "}}"'
                        )
                    k_sub_alts.append(k_sub)
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(f'{rule_name} ::= ' + ' | '.join(k_sub_alts))
            else:
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(
                    f'{rule_name} ::= "→{{R:{rec}(RTK)@" region "}}"'
                    f' "→{{K:" code state? "@" region "}}"'
                    f' "→{{TF:" code state? "@" region "}}"'
                )
            receptor_alts.append(rule_name)

        # JAK-STAT: L→R(JAK-STAT)→K(JAK)→TF(STAT)  (JAK constrained by receptor, STAT constrained by JAK)
        for rec, coupling, rec_safe in jakstat_alts:
            valid_jaks = JAKSTAT_KINASES.get(rec, [])
            if valid_jaks:
                jak_sub_alts = []
                for jak in valid_jaks:
                    jak_safe = safe_name(jak)
                    valid_stats = KINASE_TARGETS.get(jak, [])
                    if valid_stats:
                        stat_options = ' | '.join(f'"{s}"' for s in valid_stats)
                        jak_sub = f'lig_{lig_safe}_rec_{rec_safe}_jak_{jak_safe}'
                        rules.append(
                            f'{jak_sub} ::= "→{{R:{rec}(JAK-STAT)@" region "}}"'
                            f' "→{{K:{jak}" state? "@" region "}}"'
                            f' "→{{TF:" ({stat_options}) state? "@" region "}}"'
                        )
                    else:
                        jak_sub = f'lig_{lig_safe}_rec_{rec_safe}_jak_{jak_safe}'
                        rules.append(
                            f'{jak_sub} ::= "→{{R:{rec}(JAK-STAT)@" region "}}"'
                            f' "→{{K:{jak}" state? "@" region "}}"'
                        )
                    jak_sub_alts.append(jak_sub)
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(f'{rule_name} ::= ' + ' | '.join(jak_sub_alts))
            else:
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(
                    f'{rule_name} ::= "→{{R:{rec}(JAK-STAT)@" region "}}"'
                    f' "→{{K:" code state? "@" region "}}"'
                    f' "→{{TF:" code state? "@" region "}}"'
                )
            receptor_alts.append(rule_name)

        # cGMP: L→R(sGC)→2m:cGMP→K:PKG (NO/gasotransmitter pathway)
        for rec, coupling, rec_safe in cgmp_alts:
            valid_kinases = SM_KINASE.get("cGMP", [])
            if valid_kinases:
                k_options = ' | '.join(f'"{k}"' for k in valid_kinases)
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(
                    f'{rule_name} ::= {edge} "{{R:{rec}@" region "}}"'
                    f' "→{{2m:cGMP" state? "@" region "}}"'
                    f' "→{{K:" ({k_options}) state? "@" region "}}"'
                )
            else:
                rule_name = f'lig_{lig_safe}_rec_{rec_safe}'
                rules.append(
                    f'{rule_name} ::= {edge} "{{R:{rec}@" region "}}"'
                    f' "→{{2m:cGMP" state? "@" region "}}"'
                )
            receptor_alts.append(rule_name)

        if receptor_alts:
            rules.append(f'lig_{lig_safe}_cascade ::= ' + ' | '.join(receptor_alts))

        all_ligand_rules.append(f'lig_{lig_safe}_cascade')
        rules.append("")

    rules.append("# All ligand cascades")
    rules.append("ligand_cascade ::= " + " | ".join(all_ligand_rules))
    rules.append("")

    # ── ENZYME → PRODUCT ──
    rules.append("# ── ENZYME-PRODUCT (knowledge graph encoded) ──")
    rules.append("")

    enzyme_alts = []
    for enzyme, product in ENZYME_PRODUCT.items():
        enz_safe = safe_name(enzyme)
        if product:
            # Determine product type
            if product in ("DA", "5HT", "NE", "GABA", "GLU", "ACh", "GLN", "NAS"):
                prod_type = "L.nt"
            elif product in ("estradiol", "T3", "rT3", "CORT", "cortisone", "melatonin", "PGE2"):
                prod_type = "L.h"
            elif product in ("allopregnanolone",):
                prod_type = "L.ns"
            elif product in ("KYN",):
                prod_type = "L.ni"
            elif product in ("NO",):
                prod_type = "L.gas"
            elif product in ("amyloid-β",):
                prod_type = "P.agg"
            else:
                prod_type = "L.nt"
            rules.append(f'enzyme_{enz_safe} ::= "{{E:{enzyme}" state? "@" region "}}→{{{prod_type}:{product}" state? "@" region "}}"')
        enzyme_alts.append(f'enzyme_{enz_safe}')

    # Degradation enzymes
    for enzyme in sorted(DEGRADATION_ENZYMES):
        enz_safe = safe_name(enzyme)
        rules.append(f'enzyme_{enz_safe} ::= "{{E:{enzyme}" state? "@" region "}}→⊘"')
        enzyme_alts.append(f'enzyme_{enz_safe}')

    rules.append(f'enzyme_chain ::= ' + ' | '.join(enzyme_alts))
    rules.append("")

    # ── TRANSPORTER → SUBSTRATE ──
    rules.append("# ── TRANSPORTER-SUBSTRATE (knowledge graph encoded) ──")
    rules.append("")

    transport_alts = []
    for transporter, substrates in TRANSPORTER_SUB.items():
        tr_safe = safe_name(transporter)
        sub_options = ' | '.join(f'"{s}"' for s in substrates)
        rules.append(f'transport_{tr_safe} ::= "{{T:{transporter}" state? "@" region "}}|>{{V:ves_" ({sub_options}) "@" region "}}"')
        transport_alts.append(f'transport_{tr_safe}')

    rules.append(f'recycling_chain ::= ' + ' | '.join(transport_alts))
    rules.append("")

    # ── GENERIC RULES (for non-constrained parts) ──
    rules.append("# ── GENERIC (unconstrained parts) ──")
    rules.append('code ::= [A-Za-z0-9α-ωβ._/-]+')
    rules.append('second_messenger ::= "cAMP" | "IP3" | "DAG" | "Ca²⁺" | "Cl⁻" | "Na⁺" | "K⁺" | "cGMP"')
    rules.append('rest_of_chain ::= [^\\n]+')
    rules.append('')

    # ── INTEGRATION ──
    rules.append('integration ::= "∫{" code "@" region "}←(" input_list ")→" code "@" region ":" mode "\\n"')
    rules.append('input_list ::= input ("," input)*')
    rules.append('input ::= code "@" region ":" sign')
    rules.append('sign ::= "+" | "-" | "×"')
    rules.append('mode ::= "thr" | "rate" | "burst" | "tonic"')
    rules.append('')

    # ── PROTOCOL ──
    rules.append('protocol ::= "{" code "@" region "}⊲{" [^}]+ "}[" pterms "]\\n"')
    rules.append('pterms ::= pterm (" " pterm)*')
    rules.append('pterm ::= "exc" | "inh" | "mod" | "fast" | "slow" | "tonic" | "syn" | "vol" | "gap" | "para" | gate_cond')
    rules.append('gate_cond ::= "{" code "@" region ">=" state_val "}"')
    rules.append('')

    # ── CONDITIONAL ──
    rules.append('conditional ::= "⊗(" condition+ ")⟹" effect "\\n"')
    rules.append('condition ::= "¬"? "{" code "@" region "}>=" state_val')
    rules.append('effect ::= "{" code "@" region "}:" effect_type')
    rules.append('effect_type ::= "pass" | "block" | "amplify" | "apoptosis" | "switch:" code')
    rules.append('')

    # ── COMPOSITE ──
    rules.append('composite ::= "◈" code "=" comp_ref ("+" comp_ref)* "\\n"')
    rules.append('comp_ref ::= "{" code "@" region "}"')
    rules.append('')

    # ── DYSREG ──
    rules.append('dysreg ::= "⚡" dysreg_type ":" [^(]+ "(" [^)]+ ")\\n"')
    rules.append('dysreg_type ::= "sus" | "dep" | "exc" | "shunt" | "osc" | "res" | "acc" | "lock" | "sat"')
    rules.append('')

    # ── OBSERVABLE ──
    rules.append('observable ::= "⊕ " code " → " obs_refs " (" obs_rel ")\\n"')
    rules.append('obs_refs ::= "{" code "@" region "}" ("," "{" code "@" region "}")*')
    rules.append('obs_rel ::= "direct" | "proxy" | "ratio" | "activity" | "metabolite" | "autonomic"')

    return '\n'.join(rules)


# ═══════════════════════════════════════
# STATS
# ═══════════════════════════════════════

def count_stats():
    binding_pairs = sum(len(v) for v in BINDINGS.items())
    total_receptors = len(set(r for pairs in BINDINGS.values() for r, c in pairs))

    cascade_rules = sum(len(v) for v in BINDINGS.values())
    enzyme_rules = len(ENZYME_PRODUCT) + len(DEGRADATION_ENZYMES)
    transport_rules = len(TRANSPORTER_SUB)

    print(f"Knowledge Graph → Grammar Stats")
    print(f"{'='*50}")
    print(f"Ligands:             {len(BINDINGS)}")
    print(f"Unique receptors:    {total_receptors}")
    print(f"Binding pairs:       {cascade_rules}")
    print(f"Enzyme rules:        {enzyme_rules}")
    print(f"Transport rules:     {transport_rules}")
    print(f"{'='*50}")
    print(f"Total grammar rules: ~{cascade_rules + enzyme_rules + transport_rules + 50}")
    print(f"(+50 for generic/structural rules)")


if __name__ == "__main__":
    import sys

    if "--stats" in sys.argv:
        count_stats()
    else:
        grammar = generate_ebnf()
        if "--out" in sys.argv:
            idx = sys.argv.index("--out")
            path = sys.argv[idx + 1]
            with open(path, "w") as f:
                f.write(grammar)
            print(f"Written to {path}")
        else:
            print(grammar)
