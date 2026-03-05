"""Bio domain boundary testing - build clean signal graph and run multi-scenario simulations."""
import json
import requests
import sys

API = "http://localhost:13370/api/kernel/simulate"

# Unique bio signals with realistic values based on clinical profile
# State arrows: ↑↑=0.8, ↑=0.65, ≈=0.5, ↓=0.35, ↓↓=0.2
SIGNALS = [
    {"Code": "5HT",    "Value": 0.20, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 50},   # serotonin ↓↓
    {"Code": "DA",     "Value": 0.65, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 30},   # dopamine ↑
    {"Code": "NE",     "Value": 0.75, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 20},   # norepinephrine ↑↑
    {"Code": "GABA",   "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 40},   # GABA ↓
    {"Code": "GLU",    "Value": 0.65, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 10},   # glutamate ↑
    {"Code": "ACh",    "Value": 0.35, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 25},   # acetylcholine ↓
    {"Code": "CORT",   "Value": 0.80, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 300},  # cortisol ↑↑ (slow)
    {"Code": "CRH",    "Value": 0.70, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 200},  # CRH ↑
    {"Code": "ACTH",   "Value": 0.65, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 150},  # ACTH ↑
    {"Code": "BDNF",   "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 500},  # BDNF ↓ (very slow)
    {"Code": "MEL",    "Value": 0.40, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 400},  # melatonin ↓~
    {"Code": "OXT",    "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 100},  # oxytocin ↓
    {"Code": "END",    "Value": 0.25, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 60},   # endorphin ↓
    {"Code": "NPY",    "Value": 0.20, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 80},   # NPY ↓↓
    {"Code": "IL6",    "Value": 0.70, "Baseline": 0.30, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 600},  # IL-6 ↑ (slow inflammatory)
    {"Code": "TNFa",   "Value": 0.65, "Baseline": 0.30, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 500},  # TNF-a ↑
    {"Code": "KYN",    "Value": 0.65, "Baseline": 0.35, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 400},  # kynurenine ↑
    {"Code": "TRP",    "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 200},  # tryptophan ↓
    {"Code": "DHEA",   "Value": 0.25, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 500},  # DHEA ↓
    {"Code": "SP",     "Value": 0.70, "Baseline": 0.40, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 50},   # substance P ↑
    {"Code": "HIST",   "Value": 0.75, "Baseline": 0.45, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 30},   # histamine ↑↑
    {"Code": "AVP",    "Value": 0.65, "Baseline": 0.45, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 150},  # vasopressin ↑
    {"Code": "PRL",    "Value": 0.60, "Baseline": 0.45, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 200},  # prolactin ↑
    {"Code": "TSH",    "Value": 0.60, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 600},  # TSH ↑ (very slow)
]

# Build code->index map
code_idx = {s["Code"]: i for i, s in enumerate(SIGNALS)}

# Edges from DB (deduplicated), mapped to indices
# Operator mapping: → = causal, ⟳⁻ = feedback (negative), ⚡ = dysreg, ⊣ = inhibit
EDGES_RAW = [
    # source, target, operator, operator_class, gain
    ("TRP",   "5HT",  "→",  "causal",   1.2),   # tryptophan → serotonin synthesis
    ("5HT",   "DA",   "⊣",  "inhibit",  0.4),   # 5HT2C tonically inhibits mesolimbic DA
    ("5HT",   "GABA", "→",  "causal",   0.8),   # 5HT promotes GABA release in PFC
    ("5HT",   "GLU",  "⊣",  "inhibit",  0.5),   # 5HT modulates glutamate
    ("GLU",   "5HT",  "→",  "causal",   0.3),   # glutamate weak excitatory on serotonin
    ("GLU",   "GABA", "⊣",  "inhibit",  0.7),   # E/I balance: GLU suppresses GABA
    ("GABA",  "GLU",  "⊣",  "inhibit",  0.7),   # E/I balance: GABA suppresses GLU (added)
    ("DA",    "NE",   "→",  "causal",   0.5),   # DA-NE pathway
    ("NE",    "GLU",  "→",  "causal",   0.6),   # NE excitatory on glutamate
    ("NE",    "CORT", "→",  "causal",   0.4),   # NE stimulates HPA (added)
    ("CRH",   "ACTH", "→",  "causal",   0.9),   # HPA axis: CRH→ACTH (added)
    ("ACTH",  "CORT", "→",  "causal",   0.9),   # HPA axis: ACTH→CORT
    ("CORT",  "CRH",  "⊣",  "inhibit",  0.6),   # negative feedback: CORT ⊣ CRH
    ("CORT",  "5HT",  "⊣",  "inhibit",  0.7),   # cortisol suppresses serotonin (via TPH)
    ("CORT",  "GABA", "⊣",  "inhibit",  0.5),   # cortisol suppresses GABA
    ("CORT",  "BDNF", "⊣",  "inhibit",  0.8),   # cortisol suppresses BDNF
    ("CORT",  "NPY",  "⊣",  "inhibit",  0.6),   # cortisol depletes NPY
    ("NPY",   "CORT", "⊣",  "inhibit",  0.5),   # NPY buffers cortisol
    ("KYN",   "5HT",  "⊣",  "inhibit",  0.6),   # kynurenine diverts from 5HT (added - replaces KYN→CORT)
    ("TNFa",  "IL6",  "→",  "causal",   0.8),   # inflammatory cascade
    ("IL6",   "KYN",  "→",  "causal",   0.7),   # inflammation drives kynurenine (added)
    ("IL6",   "CORT", "→",  "causal",   0.4),   # inflammation stimulates HPA (added)
    ("CORT",  "TNFa", "⊣",  "inhibit",  0.3),   # cortisol anti-inflammatory (added)
    ("5HT",   "MEL",  "→",  "causal",   0.7),   # serotonin → melatonin synthesis (added)
    ("BDNF",  "5HT",  "→",  "causal",   0.4),   # BDNF supports serotonergic function (added)
    ("END",   "GABA", "→",  "causal",   0.5),   # endorphins potentiate GABA (added)
    ("OXT",   "CORT", "⊣",  "inhibit",  0.4),   # oxytocin buffers cortisol (added)
]

EDGES = []
for src, tgt, op, op_class, gain in EDGES_RAW:
    if src in code_idx and tgt in code_idx:
        EDGES.append({
            "SourceIdx": code_idx[src],
            "TargetIdx": code_idx[tgt],
            "Operator": op,
            "OperatorClass": op_class,
            "Gain": gain,
            "NoiseSigma": 0.02,
            "TransferFn": "sig" if op_class == "inhibit" else "lin",
        })

# Gates: stress-threshold gate on CRH→ACTH
GATES = [
    {"Code": "STRESS_GATE", "Type": "threshold", "Threshold": 0.6},  # HPA fires above 0.6
]
# Attach gate to CRH→ACTH edge
for e in EDGES:
    if e["SourceIdx"] == code_idx["CRH"] and e["TargetIdx"] == code_idx["ACTH"]:
        e["GateId"] = 0

# Define scenarios
SCENARIOS = {
    "baseline": [],
    "ssri": [
        {"SignalCode": "5HT", "Value": 0.65, "Confidence": 0.9},  # SSRI raises serotonin
    ],
    "acute_stress": [
        {"SignalCode": "CRH", "Value": 0.95, "Confidence": 0.95},  # massive CRH spike
        {"SignalCode": "NE",  "Value": 0.90, "Confidence": 0.9},   # NE surge
    ],
    "exercise": [
        {"SignalCode": "END",  "Value": 0.75, "Confidence": 0.85},  # endorphin release
        {"SignalCode": "BDNF", "Value": 0.65, "Confidence": 0.8},   # BDNF boost
        {"SignalCode": "CORT", "Value": 0.55, "Confidence": 0.7},   # mild acute cortisol then drop
    ],
    "inflammation": [
        {"SignalCode": "IL6",  "Value": 0.90, "Confidence": 0.95},  # IL-6 spike
        {"SignalCode": "TNFa", "Value": 0.85, "Confidence": 0.9},   # TNF-a spike
    ],
    "sleep_restore": [
        {"SignalCode": "MEL",  "Value": 0.75, "Confidence": 0.85},  # melatonin supplementation
        {"SignalCode": "HIST", "Value": 0.30, "Confidence": 0.8},   # antihistamine
    ],
    "social_bonding": [
        {"SignalCode": "OXT",  "Value": 0.80, "Confidence": 0.9},   # oxytocin release
    ],
    "full_recovery": [
        {"SignalCode": "5HT",  "Value": 0.65, "Confidence": 0.9},   # SSRI
        {"SignalCode": "END",  "Value": 0.70, "Confidence": 0.85},   # exercise
        {"SignalCode": "BDNF", "Value": 0.60, "Confidence": 0.8},    # exercise
        {"SignalCode": "OXT",  "Value": 0.70, "Confidence": 0.85},   # social support
        {"SignalCode": "MEL",  "Value": 0.70, "Confidence": 0.85},   # sleep hygiene
    ],
}

TICKS = 10

results = {}
for name, injections in SCENARIOS.items():
    payload = {
        "Signals": SIGNALS,
        "Edges": EDGES,
        "Gates": GATES,
        "Inject": injections if injections else None,
        "Ticks": TICKS,
        "TickIntervalMs": 100,
    }

    r = requests.post(API, json=payload, timeout=30)
    if r.status_code != 200:
        print(f"FAIL {name}: {r.status_code} {r.text[:200]}")
        continue

    data = r.json()
    results[name] = data.get("finalState", {})
    print(f"OK {name}: events={len(data.get('events', []))}")

# Print comparison table
print("\n" + "="*120)
print(f"{'Signal':<8}", end="")
for name in SCENARIOS:
    print(f" {name:>14}", end="")
print(f" {'spread':>8}")
print("-"*120)

all_signals = sorted(set(s["Code"] for s in SIGNALS))
for sig in all_signals:
    vals = [results[name].get(sig, 0) for name in SCENARIOS if name in results]
    spread = max(vals) - min(vals) if vals else 0
    print(f"{sig:<8}", end="")
    for name in SCENARIOS:
        if name in results:
            v = results[name].get(sig, 0)
            print(f" {v:>14.4f}", end="")
    print(f" {spread:>8.4f}")

# Highlight key differentiators
print("\n" + "="*80)
print("TOP DIFFERENTIATORS (by spread):")
print("-"*80)
spreads = []
for sig in all_signals:
    vals = [results[name].get(sig, 0) for name in SCENARIOS if name in results]
    if vals:
        spreads.append((sig, max(vals) - min(vals), vals))
spreads.sort(key=lambda x: -x[1])
for sig, spread, vals in spreads[:15]:
    baseline_v = results.get("baseline", {}).get(sig, 0)
    print(f"  {sig:<8} spread={spread:.4f}  baseline={baseline_v:.4f}")
    for name in SCENARIOS:
        if name != "baseline" and name in results:
            v = results[name].get(sig, 0)
            delta = v - baseline_v
            pct = (delta / baseline_v * 100) if baseline_v != 0 else 0
            if abs(delta) > 0.001:
                print(f"    {name:>16}: {v:.4f} ({delta:+.4f}, {pct:+.1f}%)")

# Print events for most interesting scenario
print("\n" + "="*80)
print("DETAILED EVENTS - SSRI scenario:")
print("-"*80)
payload = {
    "Signals": SIGNALS,
    "Edges": EDGES,
    "Gates": GATES,
    "Inject": SCENARIOS["ssri"],
    "Ticks": TICKS,
    "TickIntervalMs": 100,
}
r = requests.post(API, json=payload, timeout=30)
data = r.json()
for ev in data.get("events", [])[:50]:
    t = ev.get("tick", "?")
    detail = ev.get("detail", "")
    print(f"  t={t} {ev.get('type','')}: {detail[:100]}")

print("\n" + "="*80)
print("DETAILED EVENTS - Acute Stress scenario:")
print("-"*80)
payload["Inject"] = SCENARIOS["acute_stress"]
r = requests.post(API, json=payload, timeout=30)
data = r.json()
for ev in data.get("events", [])[:50]:
    t = ev.get("tick", "?")
    detail = ev.get("detail", "")
    print(f"  t={t} {ev.get('type','')}: {detail[:100]}")
