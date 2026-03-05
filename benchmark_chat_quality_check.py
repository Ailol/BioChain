"""
Quality evaluation: run optimized biochain-chat through full tool-calling pipeline
and evaluate each response against DB ground truth in detail.
"""
import requests, json, time, re, textwrap

OLLAMA = "http://localhost:11434/api/chat"
MODEL = "biochain-chat:latest"

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

TOOL_RESPONSES = {
    "get_signal_register": open("signal_register.txt").read() if False else """NT:5HT[↓↓] @PFC effective=-2.0 drift=-2
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

SYSTEM_PROMPT = """You are a BioChain biochemical analyst. You have deep expertise in neurochemistry,
psychopharmacology, and the BioChain signal notation system.

You have tools to query a person's biochemical profile from the BioChain database.
Use these tools to fetch exactly the data you need to answer the user's question.
Do not guess — always query the database first.

Use BioChain notation in your responses:
  Signal states: ↑↑ ↑ ≈ ↓ ↓↓
  Layers: NT(neurotransmitter) H(hormone) P(peptide) eCB(endocannabinoid) NI(neuroimmune) NS(neurosteroid)

Connect biochemical mechanisms to observable behavior and subjective experience.
Be specific about signal interactions, cascade effects, and intervention targets."""

PERSON_ID = "11111111-1111-1111-1111-111111111111"

PROMPTS = [
    "Give me an overview of this person's biochemical state. What are the most critical signals and what do they mean for daily experience?",
    "What is the system's resilience? How stable is it and what could push it further into crisis?",
    "What dysregulations are present? Explain the mechanisms and what behavioral symptoms they'd produce.",
]

# Ground truth checks
GROUND_TRUTH = {
    "critical_signals": {
        "5HT": {"state": "↓↓", "regions": ["PFC", "HYP"], "fact": "severely depleted serotonin"},
        "CORT": {"state": "↑↑", "regions": ["ADR"], "fact": "cortisol hypersecretion via HPA axis"},
        "CRH": {"state": "↑↑", "regions": ["PVN"], "fact": "CRH drives cortisol, stress axis"},
        "GLU": {"state": "↑↑", "regions": ["AMY", "cortex"], "fact": "glutamate excitotoxicity risk"},
        "GABA": {"state": "↓", "regions": ["AMY", "VLPO", "PAG", "HYP"], "fact": "inhibitory deficit"},
        "2AG": {"state": "↓↓", "regions": ["NAc"], "fact": "endocannabinoid system collapsed"},
        "ANA": {"state": "↓↓", "regions": ["NAc"], "fact": "anandamide depleted"},
        "DA": {"state": "↑", "regions": ["PFC"], "fact": "mild PFC dopamine elevation"},
        "NE": {"state": "↓", "regions": ["LC"], "fact": "norepinephrine depletion"},
    },
    "resilience": {
        "class": "compromised",
        "ratio": "0.02",
        "failing_negative": "36",
        "dysreg_count": "11",
    },
    "dysregs": ["excitotoxicity", "depletion", "resistance", "spillover", "accumulation", "uncoupling"],
    "causal_chains": [
        "CORT→5HT uncoupling",
        "GLU→NMDA excitotoxicity",
        "5HT depletion via TPH2",
        "NE spillover → autoreceptor desensitization",
        "GLU→NO→oxidative stress",
    ],
}


def strip_think(text):
    while "<think>" in text and "</think>" in text:
        s = text.index("<think>")
        e = text.index("</think>") + len("</think>")
        text = (text[:s] + text[e:]).strip()
    if text.startswith("</think>"):
        text = text[len("</think>"):].strip()
    return text


def run_prompt(prompt_text, max_rounds=3):
    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "system", "content": f"You are analyzing: BenchmarkPerson (id: {PERSON_ID})"},
        {"role": "user", "content": prompt_text},
    ]
    tool_calls = []
    total_tokens = 0
    start = time.time()

    for _ in range(max_rounds):
        resp = requests.post(OLLAMA, json={
            "model": MODEL, "stream": False,
            "options": {"num_predict": 4096},
            "messages": messages, "tools": TOOLS,
        }, timeout=120)
        d = resp.json()
        msg = d.get("message", {})
        total_tokens += d.get("eval_count", 0)
        tc = msg.get("tool_calls", [])
        content = msg.get("content", "")

        if tc:
            messages.append(msg)
            for call in tc:
                fn = call.get("function", {}).get("name", "")
                tool_calls.append(fn)
                mock = TOOL_RESPONSES.get(fn, f"No data for {fn}.")
                messages.append({"role": "tool", "content": mock})
        else:
            break

    wall = time.time() - start
    final = strip_think(content) if content else ""
    return {"text": final, "tools": tool_calls, "tokens": total_tokens, "time": wall}


def evaluate(prompt_idx, response_text):
    """Detailed quality evaluation against ground truth."""
    text = response_text
    lower = text.lower()
    checks = []

    # Data Fidelity: does the model accurately represent DB data?
    fidelity_hits = 0
    fidelity_total = 0
    for sig, info in GROUND_TRUTH["critical_signals"].items():
        fidelity_total += 1
        if sig in text or sig.lower() in lower:
            if info["state"] in text:
                fidelity_hits += 1
                checks.append(f"  ✓ {sig}[{info['state']}] — correct state")
            else:
                checks.append(f"  ~ {sig} mentioned but state not shown as {info['state']}")
                fidelity_hits += 0.5
        else:
            checks.append(f"  ✗ {sig} — NOT MENTIONED")

    # Hallucination check: signals claimed at states they don't have
    hallucinations = []
    # Check if model invents signals not in DB
    invented_patterns = re.findall(r'([A-Z]{2,6})\[([↑↓≈]{1,2})\]', text)
    known_signals = set(GROUND_TRUTH["critical_signals"].keys())
    known_signals.update(["ACh", "ADEN", "ALDO", "ALLO", "DYN", "ENK", "GAL", "GHR", "GLU",
                          "HIST", "IL1b", "INS", "KYN", "LEP", "MEL", "NO", "NOCI", "NTS",
                          "ORX", "PREG", "PROG", "SAMe", "SP", "TAU", "THDOC", "TNFa", "bEND",
                          "PEA", "eCB", "IFNg", "IL6", "QUIN", "BDNF", "NPY", "OXT", "VIP", "DHEA"])
    for sig, state in invented_patterns:
        if sig not in known_signals:
            hallucinations.append(f"{sig}[{state}]")

    # Resilience data check
    res_checks = []
    res = GROUND_TRUTH["resilience"]
    for key, val in res.items():
        if val in text:
            res_checks.append(f"  ✓ {key}={val}")
        else:
            res_checks.append(f"  ✗ {key}={val} — NOT MENTIONED")

    # Dysreg mechanism check
    dysreg_hits = sum(1 for d in GROUND_TRUTH["dysregs"] if d.lower() in lower)
    causal_hits = sum(1 for c in GROUND_TRUTH["causal_chains"] if any(
        w in lower for w in c.lower().split()))

    # Behavioral connection check
    behavioral_words = ["anxiety", "stress", "sleep", "fatigue", "mood", "motivation",
                        "attention", "memory", "pain", "inflammation", "depression",
                        "arousal", "reward", "energy", "cognitive", "emotional"]
    behavioral_count = sum(1 for w in behavioral_words if w in lower)

    # Notation usage
    notation_count = sum(text.count(s) for s in ["↑↑", "↑", "≈", "↓", "↓↓"])

    # Structure
    has_headers = bool(re.findall(r'^#+\s|^\*\*', text, re.MULTILINE))
    has_lists = bool(re.findall(r'^[-*•]\s', text, re.MULTILINE))

    fidelity_score = fidelity_hits / max(fidelity_total, 1) * 10
    hallucination_penalty = min(len(hallucinations) * 1.5, 3)
    dysreg_score = dysreg_hits / len(GROUND_TRUTH["dysregs"]) * 10
    behavioral_score = min(behavioral_count / 5, 1.0) * 10
    notation_score = min(notation_count / 10, 1.0) * 10

    overall = (fidelity_score * 0.30 + dysreg_score * 0.20 + behavioral_score * 0.20 +
               notation_score * 0.15 + (10 - hallucination_penalty) * 0.15)

    return {
        "fidelity": fidelity_score,
        "fidelity_checks": checks,
        "hallucinations": hallucinations,
        "hallucination_penalty": hallucination_penalty,
        "dysreg_score": dysreg_score,
        "dysreg_hits": dysreg_hits,
        "behavioral_score": behavioral_score,
        "behavioral_count": behavioral_count,
        "notation_score": notation_score,
        "notation_count": notation_count,
        "res_checks": res_checks,
        "has_headers": has_headers,
        "has_lists": has_lists,
        "word_count": len(text.split()),
        "overall": overall,
    }


def main():
    print("=" * 120)
    print("QUALITY EVALUATION: Optimized biochain-chat (rp1.1 + mp0.05)")
    print("=" * 120)

    # Warm up
    requests.post(OLLAMA, json={"model": MODEL, "stream": False, "options": {"num_predict": 10},
        "messages": [{"role": "user", "content": "test"}]}, timeout=60)

    results = []
    for i, prompt in enumerate(PROMPTS):
        print(f"\n{'─' * 120}")
        print(f"PROMPT {i+1}: {prompt[:80]}...")
        print(f"{'─' * 120}")

        r = run_prompt(prompt)
        print(f"Tools called: {r['tools']}")
        print(f"Tokens: {r['tokens']}  Time: {r['time']:.1f}s  Words: {len(r['text'].split())}")

        if not r['text'].strip():
            print("!! EMPTY RESPONSE — model got stuck in tool loop")
            results.append({"prompt": i+1, "overall": 0, "empty": True})
            continue

        print(f"\n--- RESPONSE ---")
        # Print response with wrapping
        for line in r['text'].split('\n'):
            if line.strip():
                print(line)
        print(f"--- END ---\n")

        ev = evaluate(i, r['text'])
        results.append({"prompt": i+1, **ev, "empty": False})

        print(f"SCORES:")
        print(f"  Data Fidelity:    {ev['fidelity']:.1f}/10  ({sum(1 for c in ev['fidelity_checks'] if '✓' in c)}/{len(ev['fidelity_checks'])} signals correct)")
        print(f"  Hallucinations:   {len(ev['hallucinations'])} found  (penalty: -{ev['hallucination_penalty']:.1f})")
        if ev['hallucinations']:
            print(f"    Invented: {', '.join(ev['hallucinations'])}")
        print(f"  Dysreg Coverage:  {ev['dysreg_score']:.1f}/10  ({ev['dysreg_hits']}/{len(GROUND_TRUTH['dysregs'])})")
        print(f"  Behavioral Link:  {ev['behavioral_score']:.1f}/10  ({ev['behavioral_count']} behavioral terms)")
        print(f"  Notation Usage:   {ev['notation_score']:.1f}/10  ({ev['notation_count']} symbols)")
        print(f"  Structure:        headers={'YES' if ev['has_headers'] else 'NO'}, lists={'YES' if ev['has_lists'] else 'NO'}")
        print(f"  ═══════════════")
        print(f"  OVERALL:          {ev['overall']:.1f}/10")
        print()
        print(f"  Signal accuracy details:")
        for c in ev['fidelity_checks']:
            print(f"    {c}")
        if ev['res_checks']:
            print(f"  Resilience data:")
            for c in ev['res_checks']:
                print(f"    {c}")

    # Summary
    valid = [r for r in results if not r.get("empty")]
    if valid:
        avg = sum(r["overall"] for r in valid) / len(valid)
        print(f"\n{'=' * 120}")
        print(f"OVERALL QUALITY: {avg:.1f}/10  (across {len(valid)} non-empty responses, {len(results) - len(valid)} empty)")
        print(f"{'=' * 120}")

        if avg >= 8:
            print("VERDICT: EXCELLENT — Production-ready quality")
        elif avg >= 6:
            print("VERDICT: GOOD — Usable with minor issues")
        elif avg >= 4:
            print("VERDICT: MODERATE — Needs improvement")
        else:
            print("VERDICT: POOR — Significant quality issues")


if __name__ == "__main__":
    main()
