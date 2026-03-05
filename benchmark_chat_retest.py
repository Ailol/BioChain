"""
Retest: Top 8 configs × 3 iterations × 3 prompts = 72 runs
Focus on statistical reliability of the best candidates from round 1.

Key finding from round 1: model has HIGH variance per run.
Many configs produce excellent output in one prompt but empty in another.
We need multiple iterations to find the CONSISTENTLY best config.
"""
import requests
import json
import time
import re
import sys
import statistics

OLLAMA = "http://localhost:11434/api/chat"
MODEL = "biochain-chat:latest"
PERSON_ID = "11111111-1111-1111-1111-111111111111"
ITERATIONS = 3

# ─── Ground truth ─────────────────────────────────────────────────────────────

CRITICAL_SIGNALS = {
    "5HT": "↓↓", "CORT": "↑↑", "CRH": "↑↑", "GLU": "↑↑",
    "GABA": "↓", "2AG": "↓↓", "ANA": "↓↓", "DA": "↑", "NE": "↓",
}

CRITICAL_REGIONS = ["PFC", "AMY", "NAc", "ADR", "HYP", "DRN", "VTA", "PVN"]

DYSREG_KEYWORDS = ["excitotoxicity", "depletion", "uncoupling", "spillover", "accumulation", "resistance"]

# ─── Tools ────────────────────────────────────────────────────────────────────

TOOLS = [
    {"type": "function", "function": {"name": "get_signal_register",
        "description": "Get all neurochemical signals with current state and effective values.",
        "parameters": {"type": "object", "properties": {}, "required": []}}},
    {"type": "function", "function": {"name": "get_drift_analysis",
        "description": "Get signals that drifted from baseline with drift magnitude.",
        "parameters": {"type": "object", "properties": {}, "required": []}}},
    {"type": "function", "function": {"name": "get_feedback_loops",
        "description": "Get feedback loops with polarity, gain, and stability status.",
        "parameters": {"type": "object", "properties": {}, "required": []}}},
    {"type": "function", "function": {"name": "get_bottlenecks",
        "description": "Get rate-limiting enzyme bottlenecks.",
        "parameters": {"type": "object", "properties": {}, "required": []}}},
    {"type": "function", "function": {"name": "get_dysregulations",
        "description": "Get detected dysregulation events with causal context.",
        "parameters": {"type": "object", "properties": {}, "required": []}}},
    {"type": "function", "function": {"name": "get_resilience",
        "description": "Get overall system resilience score.",
        "parameters": {"type": "object", "properties": {}, "required": []}}},
    {"type": "function", "function": {"name": "simulate_cascade",
        "description": "Simulate cascade propagation from a signal stimulus.",
        "parameters": {"type": "object", "properties": {
            "signalCode": {"type": "string"}, "state": {"type": "string"}
        }, "required": ["signalCode", "state"]}}},
    {"type": "function", "function": {"name": "simulate_intervention",
        "description": "Predict effect of changing a component.",
        "parameters": {"type": "object", "properties": {
            "componentType": {"type": "string"}, "componentCode": {"type": "string"}, "newState": {"type": "string"}
        }, "required": ["componentType", "componentCode", "newState"]}}},
]

# ─── Tool responses ───────────────────────────────────────────────────────────

TOOL_RESPONSES = {
    "get_signal_register": """NT:5HT[↓↓] @PFC effective=-2.0 drift=-2
NT:5HT[↓↓] @HYP effective=-2.0 drift=-2
NT:5HT[≈] @DRN effective=0.0
NT:ACh[↓] @BF effective=-1.0 drift=-1
NT:ADEN[↑↑] @VLPO effective=2.0 drift=+2
NT:ADEN[↑] @cortex effective=1.0 drift=+1
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
NT:GLU[↑↑] @AMY effective=2.0 drift=+2
NT:GLU[↑] @PFC effective=1.0 drift=+1
NT:GLU[↑↑] @cortex effective=2.0 drift=+2
NT:INS[↑] @HYP effective=1.0 drift=+1
NT:KYN[↑] @cortex effective=1.0 drift=+1
NT:KYN[↑↑] effective=2.0 drift=+2
NT:LEP[↑] @HYP effective=1.0 drift=+1
NT:MEL[↓] @SCN effective=-1.0 drift=-1
NT:NE[↓] @LC effective=-1.0 drift=-1
NT:ORX[↓] @HYP effective=-1.0 drift=-1
NT:SP[↑] @AMY effective=1.0 drift=+1
NT:TNFa[↑] @periphery effective=1.0 drift=+1
NT:bEND[↓] @PAG effective=-1.0 drift=-1
eCB:2AG[↓↓] @NAc effective=-2.0 drift=-2
eCB:ANA[↓↓] @NAc effective=-2.0 drift=-2
eCB:eCB[≈] @PFC effective=0.0
eCB:PEA[↑] @NAc effective=1.0 drift=+1
NI:IFNg[↑] @CNS effective=1.0 drift=+1
NI:IL1b[↑] @HPC effective=1.0 drift=+1
NI:IL6[↑] @periphery effective=1.0 drift=+1
NI:QUIN[↑] @cortex effective=1.0 drift=+1
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
  affected= upstream_pressure=0 feedback_involvement=0 cascade_peers=5""",

    "get_drift_analysis": """5HT[↓↓→↓↓] @PFC drift=-2.0 (severe)
5HT[↓↓→↓↓] @HYP drift=-2.0 (severe)
CORT[↑↑→↑↑] @ADR drift=+2.0 (severe)
GLU[↑↑→↑↑] @AMY drift=+2.0 (severe)
GLU[↑↑→↑↑] @cortex drift=+2.0 (severe)
CRH[↑↑→↑↑] @PVN drift=+2.0 (severe)
KYN[↑↑→↑↑] drift=+2.0 (severe)
2AG[↓↓→↓↓] @NAc drift=-2.0 (severe)
ANA[↓↓→↓↓] @NAc drift=-2.0 (severe)
ADEN[↑↑→↑↑] @VLPO drift=+2.0 (severe)""",

    "get_feedback_loops": """All 36 negative feedback loops are FAILING.
6 positive feedback loops are NEUTRAL.
No working stabilization.""",

    "get_bottlenecks": "No rate-limiting bottlenecks found.",

    "simulate_cascade": """step 1: CORT[↑↑] @ADR val=2.0 via direct
step 2: 5HT[↓↓] @DRN val=-2.0 via CORT→5HT uncoupling
step 3: GABA[↓] @AMY val=-1.0 via 5HT→GABA
step 4: GLU[↑↑] @AMY val=2.0 via GABA↓→GLU disinhibition""",

    "simulate_intervention": """5HT@PFC: -2.0→0.5 (delta=+2.5) [↑]
5HT@HYP: -2.0→0.5 (delta=+2.5) [↑]
GABA@AMY: -1.0→0.0 (delta=+1.0) [≈]""",
}

SYSTEM_PROMPT = """You are a BioChain biochemical analyst with deep expertise in neurochemistry and psychopharmacology.

You have tools to query a person's biochemical profile. Use tools first, then analyze.
Do not guess — always query the database.

Use BioChain notation: ↑↑ ↑ ≈ ↓ ↓↓
Layers: NT(neurotransmitter) H(hormone) P(peptide) eCB(endocannabinoid) NI(neuroimmune)

Be specific about signal interactions, cascade effects, and intervention targets.
Connect biochemistry to observable behavior."""

TEST_PROMPTS = [
    {"id": "Q1_overview",
     "prompt": "Give me an overview of this person's biochemical state. What are the most critical signals?",
     "expected_tools": ["get_signal_register"]},
    {"id": "Q2_resilience",
     "prompt": "What is the system's resilience? How stable is it?",
     "expected_tools": ["get_resilience"]},
    {"id": "Q3_dysreg",
     "prompt": "What dysregulations are present? Explain the most dangerous ones.",
     "expected_tools": ["get_dysregulations"]},
]

# ─── Top 8 configs from round 1, plus a few new combos ───────────────────────

CONFIGS = [
    {"label": "baseline",               "options": {}},
    {"label": "t0.4+mp0.01",            "options": {"temperature": 0.4, "min_p": 0.01}},
    {"label": "t0.6+tk40+mp0.01",       "options": {"temperature": 0.6, "top_k": 40, "min_p": 0.01}},
    {"label": "rp1.1",                  "options": {"repeat_penalty": 1.1}},
    {"label": "mp0.05",                 "options": {"min_p": 0.05}},
    {"label": "hf_community",           "options": {"temperature": 0.6, "top_k": 40, "min_p": 0.01, "repeat_penalty": 1.0}},
    {"label": "mp0.01",                 "options": {"min_p": 0.01}},
    {"label": "t0.4+mp0.01+rp1.05",     "options": {"temperature": 0.4, "min_p": 0.01, "repeat_penalty": 1.05}},
    # New combos inspired by round 1 insights
    {"label": "t0.4+mp0.01+tk40",       "options": {"temperature": 0.4, "min_p": 0.01, "top_k": 40}},
    {"label": "t0.5+mp0.01",            "options": {"temperature": 0.5, "min_p": 0.01}},
    {"label": "t0.3+mp0.01",            "options": {"temperature": 0.3, "min_p": 0.01}},
    {"label": "t0.4+mp0.01+rp1.0",      "options": {"temperature": 0.4, "min_p": 0.01, "repeat_penalty": 1.0}},
]

# ─── Scoring ──────────────────────────────────────────────────────────────────

def strip_think(text: str) -> str:
    while "<think>" in text and "</think>" in text:
        s = text.index("<think>")
        e = text.index("</think>") + len("</think>")
        text = (text[:s] + text[e:]).strip()
    if text.startswith("</think>"):
        text = text[len("</think>"):].strip()
    return text


def score_response(prompt_id: str, response: str, tool_calls: list[str], expected_tools: list[str]) -> dict:
    """Unified scoring function returning 0-1 scores for each dimension."""
    resp_lower = response.lower()
    words = response.split()
    word_count = len(words)

    # 1. Tool accuracy (0-1)
    called = set(tool_calls)
    expected = set(expected_tools)
    correct = called & expected
    tool_precision = len(correct) / max(len(called), 1)
    tool_recall = len(correct) / max(len(expected), 1)
    tool_score = (tool_precision + tool_recall) / 2

    # 2. Has substantive response (0 or 1)
    has_response = 1.0 if word_count >= 30 else 0.0

    # 3. Signal coverage (how many critical signals mentioned)
    signals_mentioned = 0
    signals_correct_state = 0
    for sig, state in CRITICAL_SIGNALS.items():
        if sig.lower() in resp_lower or sig in response:
            signals_mentioned += 1
            if state in response:
                signals_correct_state += 1
    signal_coverage = signals_mentioned / len(CRITICAL_SIGNALS)
    signal_accuracy = signals_correct_state / max(signals_mentioned, 1)

    # 4. Region coverage
    regions_mentioned = sum(1 for r in CRITICAL_REGIONS if r in response)
    region_coverage = regions_mentioned / len(CRITICAL_REGIONS)

    # 5. Dysregulation coverage
    dysreg_mentioned = sum(1 for d in DYSREG_KEYWORDS if d.lower() in resp_lower)
    dysreg_coverage = dysreg_mentioned / len(DYSREG_KEYWORDS)

    # 6. Resilience accuracy
    resilience_score = 0.0
    if "compromised" in resp_lower:
        resilience_score += 0.3
    if "36" in response or "thirty-six" in resp_lower:
        resilience_score += 0.2
    if "failing" in resp_lower:
        resilience_score += 0.2
    if "11" in response and ("dysreg" in resp_lower or "disruption" in resp_lower):
        resilience_score += 0.15
    if "0.02" in response or "ratio" in resp_lower:
        resilience_score += 0.15

    # 7. Notation usage
    notation_symbols = ["↑↑", "↑", "≈", "↓", "↓↓"]
    notation_count = sum(1 for s in notation_symbols if s in response)
    notation_score = min(notation_count / 4, 1.0)

    # 8. Mechanism explanation (causal language)
    mechanism_words = ["because", "causes", "leads to", "results in", "drives",
                       "via", "through", "pathway", "cascade", "chronic",
                       "→", "←", "inhibit", "suppress", "deplet", "excitotox"]
    mechanism_count = sum(1 for w in mechanism_words if w.lower() in resp_lower)
    mechanism_score = min(mechanism_count / 4, 1.0)

    # 9. Repetition penalty
    lines = [l.strip() for l in response.split("\n") if l.strip() and len(l.strip()) > 10]
    line_counts = {}
    for l in lines:
        line_counts[l] = line_counts.get(l, 0) + 1
    repeat_lines = sum(1 for c in line_counts.values() if c >= 3)
    repeat_penalty = max(0, 1.0 - repeat_lines * 0.3)

    # 10. Structure (headers, lists)
    has_structure = 1.0 if bool(re.findall(r'^[#*-]|\*\*', response, re.MULTILINE)) else 0.5

    # Prompt-specific composite
    if prompt_id == "Q1_overview":
        composite = (tool_score * 0.15 + has_response * 0.15 + signal_coverage * 0.25 +
                     signal_accuracy * 0.15 + region_coverage * 0.10 + notation_score * 0.10 +
                     repeat_penalty * 0.05 + has_structure * 0.05)
    elif prompt_id == "Q2_resilience":
        composite = (tool_score * 0.15 + has_response * 0.15 + resilience_score * 0.35 +
                     mechanism_score * 0.15 + notation_score * 0.10 + repeat_penalty * 0.05 +
                     has_structure * 0.05)
    elif prompt_id == "Q3_dysreg":
        composite = (tool_score * 0.15 + has_response * 0.15 + dysreg_coverage * 0.25 +
                     mechanism_score * 0.20 + notation_score * 0.10 + signal_coverage * 0.05 +
                     repeat_penalty * 0.05 + has_structure * 0.05)
    else:
        composite = (tool_score + has_response + signal_coverage + notation_score) / 4

    return {
        "composite": composite,
        "tool_score": tool_score,
        "has_response": has_response,
        "signal_coverage": signal_coverage,
        "signal_accuracy": signal_accuracy,
        "region_coverage": region_coverage,
        "dysreg_coverage": dysreg_coverage,
        "resilience_score": resilience_score,
        "notation_score": notation_score,
        "mechanism_score": mechanism_score,
        "repeat_penalty": repeat_penalty,
        "has_structure": has_structure,
        "word_count": word_count,
        "repeat_lines": repeat_lines,
    }


# ─── Runner ───────────────────────────────────────────────────────────────────

def run_single(prompt_cfg: dict, options: dict) -> dict:
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "system", "content": f"You are analyzing: BenchmarkPerson (id: {PERSON_ID})"},
        {"role": "user", "content": prompt_cfg["prompt"]},
    ]
    merged = {"num_predict": 4096, **options}
    tool_calls_made = []
    start = time.time()
    total_tokens = 0

    for _ in range(3):  # max 3 tool rounds
        try:
            resp = requests.post(OLLAMA, json={
                "model": MODEL, "stream": False,
                "options": merged, "messages": messages, "tools": TOOLS,
            }, timeout=120)
            d = resp.json()
        except Exception as e:
            return {"error": str(e), "tool_calls": [], "response": "", "tokens": 0, "time": 0, "scores": {}}

        msg = d.get("message", {})
        total_tokens += d.get("eval_count", 0)
        tc = msg.get("tool_calls", [])
        content = msg.get("content", "")

        if tc:
            messages.append(msg)
            for call in tc:
                fn = call.get("function", {}).get("name", "")
                tool_calls_made.append(fn)
                mock = TOOL_RESPONSES.get(fn, f"No data for {fn}.")
                messages.append({"role": "tool", "content": mock})
        else:
            break

    wall = time.time() - start
    final = strip_think(content) if content else ""
    scores = score_response(prompt_cfg["id"], final, tool_calls_made, prompt_cfg["expected_tools"])

    return {
        "tool_calls": tool_calls_made,
        "response": final,
        "tokens": total_tokens,
        "time": wall,
        "scores": scores,
    }


def main():
    print("=" * 130)
    print(f"RETEST: Top configs × {ITERATIONS} iterations — Statistical reliability")
    print(f"Model: {MODEL}  |  Prompts: {len(TEST_PROMPTS)}  |  Configs: {len(CONFIGS)}  |  Total runs: {len(CONFIGS) * len(TEST_PROMPTS) * ITERATIONS}")
    print("=" * 130)

    # Warm up
    print("\nWarming up...")
    requests.post(OLLAMA, json={"model": MODEL, "stream": False, "options": {"num_predict": 10},
        "messages": [{"role": "user", "content": "test"}]}, timeout=60)
    print("Ready.\n")

    # all_data[config_label][prompt_id] = list of score dicts
    all_data = {c["label"]: {p["id"]: [] for p in TEST_PROMPTS} for c in CONFIGS}

    for ci, cfg in enumerate(CONFIGS):
        label = cfg["label"]
        opts = cfg["options"]
        print(f"\n[{ci+1}/{len(CONFIGS)}] {label}  ({json.dumps(opts)})")

        for it in range(ITERATIONS):
            for prompt_cfg in TEST_PROMPTS:
                pid = prompt_cfg["id"]
                r = run_single(prompt_cfg, opts)
                s = r["scores"]
                all_data[label][pid].append(s)

                tc_str = ",".join(r["tool_calls"][:3]) if r["tool_calls"] else "NONE"
                flags = []
                if s.get("has_response", 0) == 0:
                    flags.append("EMPTY")
                if s.get("repeat_lines", 0) > 0:
                    flags.append(f"RPT={s['repeat_lines']}")
                flag_str = f"  !! {','.join(flags)}" if flags else ""
                print(f"  iter{it+1} {pid:15s}  comp={s['composite']:.3f}  tool={s['tool_score']:.2f}  "
                      f"sig={s['signal_coverage']:.2f}  nota={s['notation_score']:.2f}  "
                      f"{r['tokens']:4d}tok  {r['time']:5.1f}s  [{tc_str}]{flag_str}")

    # ─── Statistical summary ──────────────────────────────────────────────────
    print("\n" + "=" * 130)
    print("STATISTICAL SUMMARY (mean ± std of composite across all prompts × iterations)")
    print("=" * 130)
    print(f"{'Config':30s}  {'Mean':>6s}  {'Std':>6s}  {'Min':>6s}  {'Max':>6s}  {'Empty%':>6s}  {'AvgTool':>7s}  {'AvgSig':>6s}  {'AvgNot':>6s}  {'Rpts':>4s}")
    print("-" * 130)

    summary = []
    for label in [c["label"] for c in CONFIGS]:
        all_composites = []
        all_tool = []
        all_sig = []
        all_nota = []
        empty_count = 0
        total_rpts = 0
        total_runs = 0

        for pid in [p["id"] for p in TEST_PROMPTS]:
            for s in all_data[label][pid]:
                all_composites.append(s["composite"])
                all_tool.append(s["tool_score"])
                all_sig.append(s["signal_coverage"])
                all_nota.append(s["notation_score"])
                if s["has_response"] == 0:
                    empty_count += 1
                total_rpts += s["repeat_lines"]
                total_runs += 1

        mean_c = statistics.mean(all_composites)
        std_c = statistics.stdev(all_composites) if len(all_composites) > 1 else 0
        min_c = min(all_composites)
        max_c = max(all_composites)
        empty_pct = empty_count / total_runs * 100
        mean_tool = statistics.mean(all_tool)
        mean_sig = statistics.mean(all_sig)
        mean_nota = statistics.mean(all_nota)

        summary.append({
            "label": label, "mean": mean_c, "std": std_c, "min": min_c, "max": max_c,
            "empty_pct": empty_pct, "tool": mean_tool, "sig": mean_sig, "nota": mean_nota, "rpts": total_rpts,
        })

    summary.sort(key=lambda x: x["mean"], reverse=True)

    for i, s in enumerate(summary):
        marker = " <<<< BEST" if i == 0 else ""
        print(f"  {s['label']:28s}  {s['mean']:.4f}  {s['std']:.4f}  {s['min']:.4f}  {s['max']:.4f}  "
              f"{s['empty_pct']:5.1f}%  {s['tool']:.4f}  {s['sig']:.4f}  {s['nota']:.4f}  {s['rpts']:4d}{marker}")

    # ─── Per-prompt breakdown for top 3 ───────────────────────────────────────
    print("\n" + "=" * 130)
    print("TOP 3 — Per-Prompt Breakdown (mean composite over iterations)")
    print("=" * 130)

    for rank, s_info in enumerate(summary[:3]):
        label = s_info["label"]
        opts_str = json.dumps(CONFIGS[[c["label"] for c in CONFIGS].index(label)]["options"])
        print(f"\n#{rank+1}: {label}  (overall mean={s_info['mean']:.4f}, std={s_info['std']:.4f})  options={opts_str}")

        for prompt_cfg in TEST_PROMPTS:
            pid = prompt_cfg["id"]
            scores = all_data[label][pid]
            composites = [s["composite"] for s in scores]
            tool_scores = [s["tool_score"] for s in scores]
            sig_scores = [s["signal_coverage"] for s in scores]
            empty = sum(1 for s in scores if s["has_response"] == 0)

            mean_c = statistics.mean(composites)
            std_c = statistics.stdev(composites) if len(composites) > 1 else 0
            mean_tool = statistics.mean(tool_scores)
            mean_sig = statistics.mean(sig_scores)

            print(f"  {pid:15s}: composite={mean_c:.3f}±{std_c:.3f}  tool={mean_tool:.2f}  signal={mean_sig:.2f}  empty={empty}/{ITERATIONS}")

    # ─── Consistency analysis ─────────────────────────────────────────────────
    print("\n" + "=" * 130)
    print("CONSISTENCY RANKING (penalizes high variance, rewards reliability)")
    print("  Score = mean - 0.5 * std - 0.2 * empty_pct/100")
    print("=" * 130)

    for s in summary:
        s["consistency"] = s["mean"] - 0.5 * s["std"] - 0.2 * (s["empty_pct"] / 100)

    summary.sort(key=lambda x: x["consistency"], reverse=True)

    print(f"{'Config':30s}  {'Consist':>7s}  {'Mean':>6s}  {'Std':>6s}  {'Empty%':>6s}")
    print("-" * 80)
    for i, s in enumerate(summary):
        marker = " <<<< MOST RELIABLE" if i == 0 else ""
        print(f"  {s['label']:28s}  {s['consistency']:.4f}  {s['mean']:.4f}  {s['std']:.4f}  {s['empty_pct']:5.1f}%{marker}")

    # ─── Save results ─────────────────────────────────────────────────────────
    best_by_mean = max(summary, key=lambda x: x["mean"])
    best_by_consistency = max(summary, key=lambda x: x["consistency"])

    save_data = {
        "model": MODEL,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "iterations": ITERATIONS,
        "total_runs": len(CONFIGS) * len(TEST_PROMPTS) * ITERATIONS,
        "best_by_mean": {"label": best_by_mean["label"], **best_by_mean},
        "best_by_consistency": {"label": best_by_consistency["label"], **best_by_consistency},
        "full_ranking": summary,
    }
    with open("benchmark_chat_retest_results.json", "w") as f:
        json.dump(save_data, f, indent=2, default=str)

    print(f"\n{'=' * 130}")
    print(f"RECOMMENDATION:")
    print(f"  Best by mean quality:  {best_by_mean['label']} (mean={best_by_mean['mean']:.4f})")
    print(f"  Best by consistency:   {best_by_consistency['label']} (consistency={best_by_consistency['consistency']:.4f})")
    print(f"{'=' * 130}")
    print(f"\nResults saved to benchmark_chat_retest_results.json")


if __name__ == "__main__":
    main()
