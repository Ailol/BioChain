"""
Benchmark: Find absolute best parameters for biochain-chat (Nanbeige4.1-3B)
Tests tool calling quality + analysis quality across parameter combinations.

Uses native Ollama /api/chat with tools, simulating the full BioChain pipeline:
  1. Model receives user query + tool definitions
  2. Model calls tool(s)
  3. We inject ground-truth DB responses
  4. Model produces final analysis
  5. We score against known data
"""
import requests
import json
import time
import re
import sys
from dataclasses import dataclass, field
from typing import Any

OLLAMA = "http://localhost:11434/api/chat"
MODEL = "biochain-chat:latest"
PERSON_ID = "11111111-1111-1111-1111-111111111111"

# ─── Ground truth from DB ─────────────────────────────────────────────────────

GROUND_TRUTH_SIGNALS = {
    # key signals with their states
    "5HT@PFC": "↓↓", "5HT@HYP": "↓↓", "5HT@DRN": "≈",
    "DA@PFC": "↑", "DA@NAc": "≈", "DA@VTA": "≈",
    "CORT@ADR": "↑↑", "CRH@PVN": "↑↑",
    "GLU@AMY": "↑↑", "GLU@PFC": "↑", "GLU@cortex": "↑↑",
    "GABA@AMY": "↓", "GABA@VLPO": "↓", "GABA@PAG": "↓", "GABA@HYP": "↓",
    "NE@LC": "↓",
    "ADEN@VLPO": "↑↑", "ADEN@cortex": "↑",
    "2AG@NAc": "↓↓", "ANA@NAc": "↓↓",
    "BDNF@HPC": "↓", "OXT@HYP": "↓", "OXT@AMY": "↓",
    "KYN@cortex": "↑", "KYN": "↑↑",
    "IL1B@periphery": "↑", "TNFa@periphery": "↑", "IL6@periphery": "↑",
    "IFNg@CNS": "↑", "QUIN@cortex": "↑",
    "INS@HYP": "↑", "LEP@HYP": "↑",
    "MEL@SCN": "↓", "ORX@HYP": "↓",
    "DHEA@ADR": "↓",
}

GROUND_TRUTH_RESILIENCE = {
    "class": "compromised",
    "ratio": 0.02,
    "working_negative": 0,
    "failing_negative": 36,
    "positive_feedback": 6,
    "bottlenecks": 0,
    "dysreg_count": 11,
}

GROUND_TRUTH_DYSREG_KEYWORDS = [
    "excitotoxicity", "depletion", "resistance", "spillover",
    "accumulation", "uncoupling", "shunt",
]

# ─── Tool definitions (matching BioChainChatService.BuildTools) ──────────────

TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "get_signal_register",
            "description": "Get all neurochemical signals with their current state, effective values, receptor modulation, clearance, and bottleneck throughput.",
            "parameters": {"type": "object", "properties": {}, "required": []}
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_drift_analysis",
            "description": "Get signals that have drifted from their baseline. Shows raw vs effective state and drift magnitude.",
            "parameters": {"type": "object", "properties": {}, "required": []}
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_feedback_loops",
            "description": "Get all feedback loops with their polarity, gain values, and stability status.",
            "parameters": {"type": "object", "properties": {}, "required": []}
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_bottlenecks",
            "description": "Get rate-limiting enzyme bottlenecks that constrain signal production.",
            "parameters": {"type": "object", "properties": {}, "required": []}
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_dysregulations",
            "description": "Get detected dysregulation events with upstream pressure, feedback involvement, and cascade peer count.",
            "parameters": {"type": "object", "properties": {}, "required": []}
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_resilience",
            "description": "Get overall system resilience score with stabilizing vs destabilizing forces.",
            "parameters": {"type": "object", "properties": {}, "required": []}
        }
    },
    {
        "type": "function",
        "function": {
            "name": "simulate_cascade",
            "description": "Simulate a cascade: inject a stimulus into a signal and see how it propagates.",
            "parameters": {
                "type": "object",
                "properties": {
                    "signalCode": {"type": "string", "description": "Signal code e.g. DA, 5HT, cortisol"},
                    "state": {"type": "string", "description": "State to inject: ↑↑, ↑, ≈, ↓, or ↓↓"}
                },
                "required": ["signalCode", "state"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "simulate_intervention",
            "description": "Simulate an intervention: predict what happens if we change one component.",
            "parameters": {
                "type": "object",
                "properties": {
                    "componentType": {"type": "string", "description": "signal, limiter, receptor, or transporter"},
                    "componentCode": {"type": "string", "description": "Component code e.g. DA, TH, D2, DAT"},
                    "newState": {"type": "string", "description": "New state e.g. ↑, ≈, active, desens"}
                },
                "required": ["componentType", "componentCode", "newState"]
            }
        }
    },
]

# ─── Mock tool responses (from actual DB) ────────────────────────────────────

TOOL_RESPONSES = {
    "get_signal_register": """NT:5HT[↓↓] @PFC effective=-2.0 drift=-2
NT:5HT[↓↓] @HYP effective=-2.0 drift=-2
NT:5HT[≈] @DRN effective=0.0
NT:ACh[↓] @BF effective=-1.0 drift=-1
NT:ADEN[↑↑] @VLPO effective=2.0 drift=+2
NT:ADEN[↑] @cortex effective=1.0 drift=+1
NT:ALDO[↑] @ADR effective=1.0 drift=+1
NT:ALLO[↓] @cortex effective=-1.0 drift=-1
NT:CORT[↑↑] @ADR effective=2.0 drift=+2
NT:DA[↑] @PFC effective=1.0 drift=+1
NT:DA[≈] @NAc effective=0.0
NT:DA[≈] @VTA effective=0.0
NT:DYN[↑] @NAc effective=1.0 drift=+1
NT:ENK[↓] @PAG effective=-1.0 drift=-1
NT:GABA[≈] @PFC effective=0.0
NT:GABA[↓] @AMY effective=-1.0 drift=-1
NT:GABA[↓] @VLPO effective=-1.0 drift=-1
NT:GABA[↓] @PAG effective=-1.0 drift=-1
NT:GABA[↓] @HYP effective=-1.0 drift=-1
NT:GAL[↓] @HYP effective=-1.0 drift=-1
NT:GHR[↓] @HYP effective=-1.0 drift=-1
NT:GLU[↑↑] @AMY effective=2.0 drift=+2
NT:GLU[↑] @PFC effective=1.0 drift=+1
NT:GLU[≈] @HPC effective=0.0
NT:GLU[↑↑] @cortex effective=2.0 drift=+2
NT:HIST[↓] @TMN effective=-1.0 drift=-1
NT:IL1b[↑] @periphery effective=1.0 drift=+1
NT:INS[↑] @HYP effective=1.0 drift=+1
NT:KYN[↑] @cortex effective=1.0 drift=+1
NT:KYN[↑↑] effective=2.0 drift=+2
NT:LEP[↑] @HYP effective=1.0 drift=+1
NT:MEL[↓] @SCN effective=-1.0 drift=-1
NT:NE[↓] @LC effective=-1.0 drift=-1
NT:NO[↑] @NAc effective=1.0 drift=+1
NT:NO[≈] @PFC effective=0.0
NT:NO[≈] @cortex effective=0.0
NT:NOCI[↑] @PAG effective=1.0 drift=+1
NT:NTS[↓] @HYP effective=-1.0 drift=-1
NT:ORX[↓] @HYP effective=-1.0 drift=-1
NT:PREG[↓] @HPC effective=-1.0 drift=-1
NT:PROG[↓] @HPC effective=-1.0 drift=-1
NT:SAMe[↓] @cortex effective=-1.0 drift=-1
NT:SP[↑] @AMY effective=1.0 drift=+1
NT:TAU[↓] @cortex effective=-1.0 drift=-1
NT:THDOC[↓] @cortex effective=-1.0 drift=-1
NT:TNFa[↑] @periphery effective=1.0 drift=+1
NT:bEND[↓] @PAG effective=-1.0 drift=-1
eCB:2AG[↓↓] @NAc effective=-2.0 drift=-2
eCB:ANA[↓↓] @NAc effective=-2.0 drift=-2
eCB:eCB[≈] @PFC effective=0.0
eCB:PEA[↑] @NAc effective=1.0 drift=+1
eCB:PEA[↓] @HYP effective=-1.0 drift=-1
NI:IFNg[↑] @CNS effective=1.0 drift=+1
NI:IL1b[↑] @HPC effective=1.0 drift=+1
NI:IL6[↑] @periphery effective=1.0 drift=+1
NI:QUIN[↑] @cortex effective=1.0 drift=+1
NI:QUIN[↑] effective=1.0 drift=+1
NI:TNFa[↑] @HPC effective=1.0 drift=+1
P:BDNF[↓] @HPC effective=-1.0 drift=-1
P:NPY[↓] @HYP effective=-1.0 drift=-1
P:OXT[↓] @HYP effective=-1.0 drift=-1
P:OXT[↓] @AMY effective=-1.0 drift=-1
P:VIP[↓] @SCN effective=-1.0 drift=-1
H:CRH[↑↑] @PVN effective=2.0 drift=+2
H:DHEA[↓] @ADR effective=-1.0 drift=-1""",

    "get_resilience": """Resilience: compromised (ratio=0.02)
Negative feedback: 0 working, 36 failing
Positive feedback: 6
Bottlenecks: 0 (0 causal deficits)
Latched gates: 0
Dysregulations: 11""",

    "get_dysregulations": """DYSREG: excitotoxicity: GLU[↑↑] → NMDA(Ca2+)[↑↑] @PFC (chronic)
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5
DYSREG: depletion: 5HT[↓] ← TPH2[↓] @DRN (chronic)
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5
DYSREG: resistance: DA.D2(Gi)[.desens] @NAc
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5
DYSREG: spillover: NE[↑↑] → alpha2.autoreceptor.desens @LC
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5
DYSREG: accumulation: GLU[↑↑] → NO[↑] → oxidative_stress @PFC
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5
DYSREG: uncoupling: CORT[↑] ⊣ 5HT @DRN
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5
DYSREG: none
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=1
DYSREG: shunt
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=1
DYSREG: resistance: ADEN.A1.desens[chronic] → ADEN.A1.upreg @cortex
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=1
DYSREG: resistance: ADEN.A2A.desens[chronic] → ADEN.A2A.upreg @STR
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=1
DYSREG: none
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=0""",

    "get_drift_analysis": """5HT[↓↓→↓↓] @PFC drift=-2.0 (severe)
5HT[↓↓→↓↓] @HYP drift=-2.0 (severe)
ADEN[↑↑→↑↑] @VLPO drift=+2.0 (severe)
CORT[↑↑→↑↑] @ADR drift=+2.0 (severe)
GLU[↑↑→↑↑] @AMY drift=+2.0 (severe)
GLU[↑↑→↑↑] @cortex drift=+2.0 (severe)
CRH[↑↑→↑↑] @PVN drift=+2.0 (severe)
KYN[↑↑→↑↑] drift=+2.0 (severe)
2AG[↓↓→↓↓] @NAc drift=-2.0 (severe)
ANA[↓↓→↓↓] @NAc drift=-2.0 (severe)
DA[↑→↑] @PFC drift=+1.0 (moderate)
ADEN[↑→↑] @cortex drift=+1.0 (moderate)
ALDO[↑→↑] @ADR drift=+1.0 (moderate)
GLU[↑→↑] @PFC drift=+1.0 (moderate)
KYN[↑→↑] @cortex drift=+1.0 (moderate)
DYN[↑→↑] @NAc drift=+1.0 (moderate)
INS[↑→↑] @HYP drift=+1.0 (moderate)
LEP[↑→↑] @HYP drift=+1.0 (moderate)
NO[↑→↑] @NAc drift=+1.0 (moderate)
NOCI[↑→↑] @PAG drift=+1.0 (moderate)
PEA[↑→↑] @NAc drift=+1.0 (moderate)
SP[↑→↑] @AMY drift=+1.0 (moderate)
IL1b[↑→↑] @periphery drift=+1.0 (moderate)
TNFa[↑→↑] @periphery drift=+1.0 (moderate)
IFNg[↑→↑] @CNS drift=+1.0 (moderate)
IL1b[↑→↑] @HPC drift=+1.0 (moderate)
IL6[↑→↑] @periphery drift=+1.0 (moderate)
QUIN[↑→↑] @cortex drift=+1.0 (moderate)
QUIN[↑→↑] drift=+1.0 (moderate)
TNFa[↑→↑] @HPC drift=+1.0 (moderate)""",

    "get_feedback_loops": """All 36 negative feedback loops are FAILING.
6 positive feedback loops are NEUTRAL.
System has no working stabilization - all negative feedback has failed.""",

    "get_bottlenecks": "No rate-limiting bottlenecks found.",

    "simulate_cascade": """step 1: CORT[↑↑] @ADR val=2.0 via direct
step 2: 5HT[↓↓] @DRN val=-2.0 via CORT→5HT uncoupling
step 3: GABA[↓] @AMY val=-1.0 via 5HT→GABA
step 4: GLU[↑↑] @AMY val=2.0 via GABA↓→GLU disinhibition""",

    "simulate_intervention": """5HT@PFC: -2.0→0.5 (delta=+2.5) [↑]
5HT@HYP: -2.0→0.5 (delta=+2.5) [↑]
GABA@AMY: -1.0→0.0 (delta=+1.0) [≈]
GLU@AMY: 2.0→1.0 (delta=-1.0) [↑]""",
}

# ─── Test prompts ─────────────────────────────────────────────────────────────

SYSTEM_PROMPT = """You are a BioChain biochemical analyst. You have deep expertise in neurochemistry,
psychopharmacology, and the BioChain signal notation system.

You have tools to query a person's biochemical profile from the BioChain database.
Use these tools to fetch exactly the data you need to answer the user's question.
Do not guess — always query the database first.

Available data:
- Signal register: current state of all neurochemical signals with modulation
- Steady state: drift analysis showing signals that deviate from baseline
- Feedback loops: negative and positive with gain and stability status
- Bottlenecks: rate-limiting enzymes constraining signal production
- Dysregulations: detected pathway disruptions with causal context
- Resilience score: overall system stability assessment
- Cascade simulation: propagate a stimulus through the network
- Intervention simulation: predict effect of changing a component

Use BioChain notation in your responses:
  Signal states: ↑↑ ↑ ≈ ↓ ↓↓ ~ ⊘
  Feedback: negative, positive
  Layers: NT(neurotransmitter) H(hormone) P(peptide) eCB(endocannabinoid) NI(neuroimmune) NS(neurosteroid)

Connect biochemical mechanisms to observable behavior and subjective experience.
Be specific about signal interactions, cascade effects, and intervention targets."""

TEST_PROMPTS = [
    {
        "id": "Q1_overview",
        "prompt": "Give me an overview of this person's biochemical state. What are the most critical signals?",
        "expected_tools": ["get_signal_register"],
        "scoring": {
            "critical_signals": ["5HT@PFC", "5HT@HYP", "CORT@ADR", "CRH@PVN", "GLU@AMY", "GLU@cortex", "GABA@AMY", "2AG@NAc", "ANA@NAc"],
            "critical_states": {"5HT": "↓↓", "CORT": "↑↑", "GLU": "↑↑", "CRH": "↑↑", "GABA": "↓", "2AG": "↓↓"},
            "must_mention_layers": ["NT", "eCB", "NI"],
        }
    },
    {
        "id": "Q2_resilience",
        "prompt": "What is the system's resilience? How stable is it?",
        "expected_tools": ["get_resilience"],
        "scoring": {
            "must_mention": ["compromised", "failing"],
            "must_state_numbers": {"failing_negative": 36, "dysreg_count": 11},
        }
    },
    {
        "id": "Q3_dysreg",
        "prompt": "What dysregulations are present? Explain the most dangerous ones.",
        "expected_tools": ["get_dysregulations"],
        "scoring": {
            "must_mention_dysregs": ["excitotoxicity", "depletion", "uncoupling", "spillover", "accumulation"],
            "must_explain_mechanism": True,
        }
    },
]

# ─── Parameter configurations to test ─────────────────────────────────────────

CONFIGS = [
    # Baseline (current Modelfile defaults)
    {"label": "baseline",               "options": {}},

    # Temperature sweep
    {"label": "temp0.3",                "options": {"temperature": 0.3}},
    {"label": "temp0.4",                "options": {"temperature": 0.4}},
    {"label": "temp0.7",                "options": {"temperature": 0.7}},
    {"label": "temp0.8",                "options": {"temperature": 0.8}},

    # min_p sweep (HF community suggestion: 0.01)
    {"label": "mp0.01",                 "options": {"min_p": 0.01}},
    {"label": "mp0.05",                 "options": {"min_p": 0.05}},
    {"label": "mp0.1",                  "options": {"min_p": 0.1}},

    # top_k sweep (HF community suggestion: 40)
    {"label": "tk20",                   "options": {"top_k": 20}},
    {"label": "tk40",                   "options": {"top_k": 40}},

    # repeat_penalty sweep
    {"label": "rp1.0",                  "options": {"repeat_penalty": 1.0}},
    {"label": "rp1.05",                 "options": {"repeat_penalty": 1.05}},
    {"label": "rp1.1",                  "options": {"repeat_penalty": 1.1}},

    # Combo: HF community best
    {"label": "hf_community",           "options": {"temperature": 0.6, "top_k": 40, "min_p": 0.01, "repeat_penalty": 1.0}},

    # Combo: lower temp + min_p
    {"label": "t0.4+mp0.01",            "options": {"temperature": 0.4, "min_p": 0.01}},
    {"label": "t0.4+mp0.05",            "options": {"temperature": 0.4, "min_p": 0.05}},

    # Combo: temp + top_k + min_p
    {"label": "t0.5+tk40+mp0.01",       "options": {"temperature": 0.5, "top_k": 40, "min_p": 0.01}},
    {"label": "t0.6+tk40+mp0.01",       "options": {"temperature": 0.6, "top_k": 40, "min_p": 0.01}},
    {"label": "t0.7+tk40+mp0.01",       "options": {"temperature": 0.7, "top_k": 40, "min_p": 0.01}},

    # Combo: repeat penalty combos
    {"label": "t0.6+rp1.05+mp0.01",     "options": {"temperature": 0.6, "repeat_penalty": 1.05, "min_p": 0.01}},
    {"label": "t0.6+rp1.0+tk40",        "options": {"temperature": 0.6, "repeat_penalty": 1.0, "top_k": 40}},

    # Combo: everything
    {"label": "full_combo_a",            "options": {"temperature": 0.6, "top_k": 40, "min_p": 0.01, "repeat_penalty": 1.0, "top_p": 0.95}},
    {"label": "full_combo_b",            "options": {"temperature": 0.5, "top_k": 40, "min_p": 0.01, "repeat_penalty": 1.05, "top_p": 0.9}},
    {"label": "full_combo_c",            "options": {"temperature": 0.4, "top_k": 20, "min_p": 0.05, "repeat_penalty": 1.05, "top_p": 0.9}},
]

# ─── Scoring functions ────────────────────────────────────────────────────────

def strip_think(text: str) -> str:
    """Remove <think>...</think> blocks."""
    while "<think>" in text and "</think>" in text:
        s = text.index("<think>")
        e = text.index("</think>") + len("</think>")
        text = (text[:s] + text[e:]).strip()
    if text.startswith("</think>"):
        text = text[len("</think>"):].strip()
    return text


def score_tool_accuracy(called_tools: list[str], expected_tools: list[str]) -> dict:
    """Score whether the model called the right tools."""
    called_set = set(called_tools)
    expected_set = set(expected_tools)

    correct = called_set & expected_set
    missed = expected_set - called_set
    extra = called_set - expected_set

    precision = len(correct) / max(len(called_set), 1)
    recall = len(correct) / max(len(expected_set), 1)

    return {
        "correct_tools": list(correct),
        "missed_tools": list(missed),
        "extra_tools": list(extra),
        "precision": precision,
        "recall": recall,
        "tool_score": (precision + recall) / 2,
    }


def score_signal_coverage(response: str, scoring: dict) -> dict:
    """Score how many critical signals the model correctly mentioned."""
    critical = scoring.get("critical_signals", [])
    states = scoring.get("critical_states", {})

    mentioned = 0
    correct_state = 0
    hallucinated = 0
    details = []

    for sig in critical:
        # Check both formats: "5HT@PFC" and "5HT[↓↓] @PFC" and "5HT ... PFC"
        code = sig.split("@")[0]
        region = sig.split("@")[1] if "@" in sig else ""

        found = False
        if code in response and (not region or region in response):
            found = True
            mentioned += 1

            # Check if correct state is mentioned near the signal
            if code in states:
                expected_state = states[code]
                # Look for the state symbol near the signal code
                if expected_state in response:
                    correct_state += 1
                    details.append(f"{sig}: CORRECT ({expected_state})")
                else:
                    details.append(f"{sig}: mentioned but wrong state")
            else:
                details.append(f"{sig}: mentioned")

        if not found:
            details.append(f"{sig}: MISSED")

    # Check for hallucinated signals (signals claimed but not in DB)
    # Simple heuristic: look for signal patterns that claim extreme states
    fake_patterns = [
        r"([A-Z]{2,6})\[↑↑\].*?@(\w+).*?effective=",
        r"([A-Z]{2,6}).*?is severely elevated",
    ]
    # We'll just count obvious fabrications

    coverage = mentioned / max(len(critical), 1)
    accuracy = correct_state / max(mentioned, 1)

    return {
        "coverage": coverage,
        "accuracy": accuracy,
        "mentioned": mentioned,
        "total": len(critical),
        "correct_state": correct_state,
        "details": details,
    }


def score_resilience(response: str, scoring: dict) -> dict:
    """Score resilience response accuracy."""
    must_mention = scoring.get("must_mention", [])
    numbers = scoring.get("must_state_numbers", {})

    mentioned = sum(1 for m in must_mention if m.lower() in response.lower())
    number_hits = 0
    for key, val in numbers.items():
        if str(val) in response:
            number_hits += 1

    mention_score = mentioned / max(len(must_mention), 1)
    number_score = number_hits / max(len(numbers), 1)

    return {
        "mention_score": mention_score,
        "number_score": number_score,
        "combined": (mention_score + number_score) / 2,
    }


def score_dysregulations(response: str, scoring: dict) -> dict:
    """Score dysregulation response accuracy."""
    must_mention = scoring.get("must_mention_dysregs", [])
    mentioned = sum(1 for d in must_mention if d.lower() in response.lower())
    coverage = mentioned / max(len(must_mention), 1)

    # Check if mechanisms are explained (look for causal language)
    mechanism_words = ["because", "causes", "leads to", "results in", "drives",
                       "via", "through", "pathway", "cascade", "chronic",
                       "→", "←", "inhibit", "suppress", "deplet"]
    mechanism_count = sum(1 for w in mechanism_words if w.lower() in response.lower())
    has_mechanism = mechanism_count >= 3

    return {
        "dysreg_coverage": coverage,
        "mentioned": mentioned,
        "total": len(must_mention),
        "has_mechanism_explanation": has_mechanism,
        "mechanism_word_count": mechanism_count,
    }


def score_quality(response: str) -> dict:
    """Score general response quality metrics."""
    lines = [l for l in response.split("\n") if l.strip()]
    words = response.split()

    # Check for BioChain notation usage
    notation_symbols = ["↑↑", "↑", "≈", "↓", "↓↓", "⟳", "⊨", "⊡"]
    notation_used = sum(1 for s in notation_symbols if s in response)

    # Check for repetition
    line_counts = {}
    for l in lines:
        ls = l.strip()
        if len(ls) > 10:
            line_counts[ls] = line_counts.get(ls, 0) + 1
    repeat_lines = sum(1 for c in line_counts.values() if c >= 3)

    # Check for coherent structure (headers, sections)
    has_headers = bool(re.findall(r'^#+\s|^[-*]\s|\*\*.*\*\*', response, re.MULTILINE))

    return {
        "word_count": len(words),
        "line_count": len(lines),
        "notation_symbols": notation_used,
        "repeat_lines": repeat_lines,
        "has_structure": has_headers,
    }


# ─── Main benchmark runner ────────────────────────────────────────────────────

def run_single(prompt_config: dict, options: dict, max_tool_rounds: int = 3) -> dict:
    """Run a single benchmark: tool call + response + scoring."""
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "system", "content": f"You are analyzing: BenchmarkPerson (id: {PERSON_ID})"},
        {"role": "user", "content": prompt_config["prompt"]},
    ]

    merged_options = {"num_predict": 4096, **options}

    tool_calls_made = []
    tool_round = 0
    start_time = time.time()
    total_tokens = 0
    tool_call_failed = False

    while tool_round < max_tool_rounds:
        try:
            resp = requests.post(OLLAMA, json={
                "model": MODEL,
                "stream": False,
                "options": merged_options,
                "messages": messages,
                "tools": TOOLS,
            }, timeout=120)
            d = resp.json()
        except Exception as e:
            return {"error": str(e), "tool_calls": [], "response": "", "tokens": 0, "time": 0}

        msg = d.get("message", {})
        total_tokens += d.get("eval_count", 0)
        tc = msg.get("tool_calls", [])
        content = msg.get("content", "")

        if tc:
            # Model wants to call tools
            messages.append(msg)
            for call in tc:
                fn_name = call.get("function", {}).get("name", "")
                tool_calls_made.append(fn_name)

                # Provide mock response
                mock = TOOL_RESPONSES.get(fn_name, f"No data available for {fn_name}.")
                messages.append({"role": "tool", "content": mock})

            tool_round += 1
        else:
            # Model produced final response
            break

    wall_time = time.time() - start_time
    final_text = strip_think(content) if content else ""

    # Score
    tool_score = score_tool_accuracy(tool_calls_made, prompt_config["expected_tools"])
    quality = score_quality(final_text)

    scoring = prompt_config.get("scoring", {})
    signal_score = {}
    resilience_score = {}
    dysreg_score = {}

    if "critical_signals" in scoring:
        signal_score = score_signal_coverage(final_text, scoring)
    if "must_mention" in scoring:
        resilience_score = score_resilience(final_text, scoring)
    if "must_mention_dysregs" in scoring:
        dysreg_score = score_dysregulations(final_text, scoring)

    return {
        "tool_calls": tool_calls_made,
        "tool_score": tool_score,
        "signal_score": signal_score,
        "resilience_score": resilience_score,
        "dysreg_score": dysreg_score,
        "quality": quality,
        "response": final_text,
        "tokens": total_tokens,
        "time": wall_time,
        "tool_call_failed": tool_call_failed,
    }


def compute_composite(results: list[dict]) -> dict:
    """Compute a composite score across all prompts for a config."""
    tool_scores = []
    content_scores = []
    total_tokens = 0
    total_time = 0
    total_repeats = 0
    total_notation = 0

    for r in results:
        # Tool accuracy
        ts = r.get("tool_score", {}).get("tool_score", 0)
        tool_scores.append(ts)

        # Content accuracy
        if r.get("signal_score"):
            content_scores.append(r["signal_score"]["coverage"] * 0.6 + r["signal_score"]["accuracy"] * 0.4)
        if r.get("resilience_score"):
            content_scores.append(r["resilience_score"]["combined"])
        if r.get("dysreg_score"):
            ds = r["dysreg_score"]
            content_scores.append(ds["dysreg_coverage"] * 0.7 + (1.0 if ds["has_mechanism_explanation"] else 0.0) * 0.3)

        total_tokens += r.get("tokens", 0)
        total_time += r.get("time", 0)
        total_repeats += r.get("quality", {}).get("repeat_lines", 0)
        total_notation += r.get("quality", {}).get("notation_symbols", 0)

    avg_tool = sum(tool_scores) / max(len(tool_scores), 1)
    avg_content = sum(content_scores) / max(len(content_scores), 1)

    # Composite: 30% tool accuracy + 50% content accuracy + 10% notation + 10% no-repeats
    notation_norm = min(total_notation / 15, 1.0)  # Expect ~5 per prompt
    repeat_penalty = max(0, 1.0 - total_repeats * 0.2)

    composite = avg_tool * 0.30 + avg_content * 0.50 + notation_norm * 0.10 + repeat_penalty * 0.10

    return {
        "composite": composite,
        "avg_tool": avg_tool,
        "avg_content": avg_content,
        "notation": total_notation,
        "repeats": total_repeats,
        "tokens": total_tokens,
        "time": total_time,
    }


def main():
    print("=" * 120)
    print("BENCHMARK: biochain-chat (Nanbeige4.1-3B) — Parameter Optimization for Tool-Calling Chat")
    print(f"Model: {MODEL}  |  Prompts: {len(TEST_PROMPTS)}  |  Configs: {len(CONFIGS)}")
    print("=" * 120)

    # Warm up
    print("\nWarming up model...")
    try:
        requests.post(OLLAMA, json={
            "model": MODEL, "stream": False,
            "options": {"num_predict": 10},
            "messages": [{"role": "user", "content": "test"}]
        }, timeout=60)
        print("Model loaded.\n")
    except Exception as e:
        print(f"WARM-UP FAILED: {e}")
        sys.exit(1)

    all_results = {}  # config_label -> list of results

    for ci, cfg in enumerate(CONFIGS):
        label = cfg["label"]
        opts = cfg["options"]
        all_results[label] = []

        print(f"\n[{ci+1}/{len(CONFIGS)}] Config: {label}  ({json.dumps(opts)})")

        for pi, prompt_cfg in enumerate(TEST_PROMPTS):
            pid = prompt_cfg["id"]
            r = run_single(prompt_cfg, opts)

            tc_str = ",".join(r["tool_calls"]) if r["tool_calls"] else "NONE"
            ts = r.get("tool_score", {}).get("tool_score", 0)

            # Get content score
            cs = 0
            if r.get("signal_score"):
                cs = r["signal_score"]["coverage"]
            elif r.get("resilience_score"):
                cs = r["resilience_score"]["combined"]
            elif r.get("dysreg_score"):
                cs = r["dysreg_score"]["dysreg_coverage"]

            q = r.get("quality", {})
            flags = []
            if q.get("repeat_lines", 0) > 0:
                flags.append(f"REPEAT={q['repeat_lines']}")
            if not r["tool_calls"]:
                flags.append("NO_TOOLS")
            if r.get("error"):
                flags.append(f"ERROR={r['error'][:30]}")
            flag_str = f"  !! {', '.join(flags)}" if flags else ""

            print(f"  {pid:15s}  tools=[{tc_str}] ts={ts:.2f}  content={cs:.2f}  "
                  f"{r['tokens']:4d}tok  {r['time']:5.1f}s  {q.get('notation_symbols',0)}nota  "
                  f"{q.get('word_count',0):3d}w{flag_str}")

            all_results[label].append(r)

    # ─── Summary table ────────────────────────────────────────────────────────
    print("\n" + "=" * 120)
    print("SUMMARY: Composite Scores (higher = better)")
    print("=" * 120)
    print(f"{'Config':30s}  {'Composite':>9s}  {'ToolAcc':>7s}  {'Content':>7s}  {'Notat':>5s}  {'Rpts':>4s}  {'Tokens':>6s}  {'Time':>6s}")
    print("-" * 120)

    ranked = []
    for label, results in all_results.items():
        comp = compute_composite(results)
        ranked.append((label, comp))

    ranked.sort(key=lambda x: x[1]["composite"], reverse=True)

    for i, (label, comp) in enumerate(ranked):
        marker = " <<<< BEST" if i == 0 else ""
        print(f"  {label:28s}  {comp['composite']:9.4f}  {comp['avg_tool']:7.3f}  {comp['avg_content']:7.3f}  "
              f"{comp['notation']:5d}  {comp['repeats']:4d}  {comp['tokens']:6d}  {comp['time']:5.1f}s{marker}")

    # ─── Top 5 detailed breakdown ─────────────────────────────────────────────
    print("\n" + "=" * 120)
    print("TOP 5 CONFIGS — Detailed Breakdown")
    print("=" * 120)

    for i, (label, comp) in enumerate(ranked[:5]):
        opts_str = json.dumps(CONFIGS[[c["label"] for c in CONFIGS].index(label)]["options"])
        print(f"\n#{i+1}: {label}  (composite={comp['composite']:.4f})  options={opts_str}")

        for pi, prompt_cfg in enumerate(TEST_PROMPTS):
            r = all_results[label][pi]
            print(f"  {prompt_cfg['id']}:")
            print(f"    Tools called: {r['tool_calls']}")
            if r.get("signal_score"):
                ss = r["signal_score"]
                print(f"    Signal coverage: {ss['mentioned']}/{ss['total']} ({ss['coverage']:.0%}), "
                      f"correct state: {ss['correct_state']}/{ss['mentioned']} ({ss['accuracy']:.0%})")
            if r.get("resilience_score"):
                rs = r["resilience_score"]
                print(f"    Resilience: mentions={rs['mention_score']:.0%}, numbers={rs['number_score']:.0%}")
            if r.get("dysreg_score"):
                ds = r["dysreg_score"]
                print(f"    Dysreg: {ds['mentioned']}/{ds['total']} ({ds['dysreg_coverage']:.0%}), "
                      f"mechanism={'YES' if ds['has_mechanism_explanation'] else 'NO'} ({ds['mechanism_word_count']} words)")

    # ─── Winner's full response ───────────────────────────────────────────────
    best_label = ranked[0][0]
    print("\n" + "=" * 120)
    print(f"WINNER: {best_label} — Full Q1 Response")
    print("=" * 120)
    best_q1 = all_results[best_label][0]
    print(f"Tools: {best_q1['tool_calls']}")
    print(f"Tokens: {best_q1['tokens']}  Time: {best_q1['time']:.1f}s")
    print("-" * 80)
    print(best_q1["response"][:3000])

    # ─── Save results ─────────────────────────────────────────────────────────
    save_data = {
        "model": MODEL,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "configs": {label: json.dumps(CONFIGS[[c["label"] for c in CONFIGS].index(label)]["options"])
                    for label in all_results},
        "ranking": [(label, comp) for label, comp in ranked],
        "best": {
            "label": best_label,
            "options": CONFIGS[[c["label"] for c in CONFIGS].index(best_label)]["options"],
            "composite": ranked[0][1]["composite"],
        }
    }
    with open("benchmark_chat_results.json", "w") as f:
        json.dump(save_data, f, indent=2, default=str)
    print(f"\nResults saved to benchmark_chat_results.json")


if __name__ == "__main__":
    main()
