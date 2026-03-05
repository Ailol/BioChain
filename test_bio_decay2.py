"""Analyze decay dynamics: run SSRI scenario at tick counts 1,2,3,5,10,20 to see convergence."""
import json
import requests

API = "http://localhost:5000/api/kernel/simulate"

SIGNALS = [
    {"Code": "5HT",  "Value": 0.20, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 50},
    {"Code": "DA",   "Value": 0.65, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 30},
    {"Code": "NE",   "Value": 0.75, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 20},
    {"Code": "GABA", "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 40},
    {"Code": "GLU",  "Value": 0.65, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 10},
    {"Code": "CORT", "Value": 0.80, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 300},
    {"Code": "CRH",  "Value": 0.70, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 200},
    {"Code": "ACTH", "Value": 0.65, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 150},
    {"Code": "BDNF", "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 500},
    {"Code": "MEL",  "Value": 0.40, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 400},
    {"Code": "IL6",  "Value": 0.70, "Baseline": 0.30, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 600},
    {"Code": "TNFa", "Value": 0.65, "Baseline": 0.30, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 500},
    {"Code": "TRP",  "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 200},
    {"Code": "NPY",  "Value": 0.20, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 80},
    {"Code": "END",  "Value": 0.25, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 60},
    {"Code": "KYN",  "Value": 0.65, "Baseline": 0.35, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 400},
    {"Code": "OXT",  "Value": 0.30, "Baseline": 0.50, "RangeLow": 0, "RangeHigh": 1.0, "TauMinMs": 100},
]

code_idx = {s["Code"]: i for i, s in enumerate(SIGNALS)}

EDGES_RAW = [
    ("TRP",  "5HT",  "→",  "causal",   1.2),
    ("5HT",  "DA",   "⊣",  "inhibit",  0.4),
    ("5HT",  "GABA", "→",  "causal",   0.8),
    ("5HT",  "GLU",  "⊣",  "inhibit",  0.5),
    ("GLU",  "GABA", "⊣",  "inhibit",  0.7),
    ("GABA", "GLU",  "⊣",  "inhibit",  0.7),
    ("DA",   "NE",   "→",  "causal",   0.5),
    ("NE",   "GLU",  "→",  "causal",   0.6),
    ("NE",   "CORT", "→",  "causal",   0.4),
    ("CRH",  "ACTH", "→",  "causal",   0.9),
    ("ACTH", "CORT", "→",  "causal",   0.9),
    ("CORT", "CRH",  "⊣",  "inhibit",  0.6),
    ("CORT", "5HT",  "⊣",  "inhibit",  0.7),
    ("CORT", "GABA", "⊣",  "inhibit",  0.5),
    ("CORT", "BDNF", "⊣",  "inhibit",  0.8),
    ("CORT", "NPY",  "⊣",  "inhibit",  0.6),
    ("NPY",  "CORT", "⊣",  "inhibit",  0.5),
    ("KYN",  "5HT",  "⊣",  "inhibit",  0.6),
    ("TNFa", "IL6",  "→",  "causal",   0.8),
    ("IL6",  "KYN",  "→",  "causal",   0.7),
    ("IL6",  "CORT", "→",  "causal",   0.4),
    ("CORT", "TNFa", "⊣",  "inhibit",  0.3),
    ("5HT",  "MEL",  "→",  "causal",   0.7),
    ("BDNF", "5HT",  "→",  "causal",   0.4),
    ("END",  "GABA", "→",  "causal",   0.5),
    ("OXT",  "CORT", "⊣",  "inhibit",  0.4),
]

EDGES = []
for src, tgt, op, op_class, gain in EDGES_RAW:
    if src in code_idx and tgt in code_idx:
        EDGES.append({
            "SourceIdx": code_idx[src], "TargetIdx": code_idx[tgt],
            "Operator": op, "OperatorClass": op_class, "Gain": gain,
            "NoiseSigma": 0.02, "TransferFn": "sig" if op_class == "inhibit" else "lin",
        })

GATES = [{"Code": "STRESS_GATE", "Type": "threshold", "Threshold": 0.6}]
for e in EDGES:
    if e["SourceIdx"] == code_idx["CRH"] and e["TargetIdx"] == code_idx["ACTH"]:
        e["GateId"] = 0

SSRI_INJECT = [{"SignalCode": "5HT", "Value": 0.65, "Confidence": 0.9}]
STRESS_INJECT = [
    {"SignalCode": "CRH", "Value": 0.95, "Confidence": 0.95},
    {"SignalCode": "NE",  "Value": 0.90, "Confidence": 0.9},
]

KEY_SIGNALS = ["5HT", "DA", "GABA", "GLU", "CORT", "CRH", "ACTH", "BDNF", "MEL", "IL6", "NE", "NPY", "KYN"]

print("="*100)
print("DECAY ANALYSIS: SSRI (5HT → 0.65) at different tick counts")
print("="*100)
print(f"{'Signal':<8} {'init':>8}", end="")
tick_counts = [1, 2, 3, 5, 10, 20]
for t in tick_counts:
    print(f" {'t='+str(t):>8}", end="")
print()
print("-"*100)

for sig in KEY_SIGNALS:
    init_val = next(s["Value"] for s in SIGNALS if s["Code"] == sig)
    print(f"{sig:<8} {init_val:>8.3f}", end="")
    for ticks in tick_counts:
        payload = {"Signals": SIGNALS, "Edges": EDGES, "Gates": GATES, "Inject": SSRI_INJECT, "Ticks": ticks}
        r = requests.post(API, json=payload, timeout=30)
        v = r.json()["finalState"].get(sig, 0)
        print(f" {v:>8.4f}", end="")
    baseline_s = next(s["Baseline"] for s in SIGNALS if s["Code"] == sig)
    print(f"  (base={baseline_s})")

print("\n" + "="*100)
print("DECAY ANALYSIS: ACUTE STRESS (CRH→0.95, NE→0.90) at different tick counts")
print("="*100)
print(f"{'Signal':<8} {'init':>8}", end="")
for t in tick_counts:
    print(f" {'t='+str(t):>8}", end="")
print()
print("-"*100)

for sig in KEY_SIGNALS:
    init_val = next(s["Value"] for s in SIGNALS if s["Code"] == sig)
    print(f"{sig:<8} {init_val:>8.3f}", end="")
    for ticks in tick_counts:
        payload = {"Signals": SIGNALS, "Edges": EDGES, "Gates": GATES, "Inject": STRESS_INJECT, "Ticks": ticks}
        r = requests.post(API, json=payload, timeout=30)
        v = r.json()["finalState"].get(sig, 0)
        print(f" {v:>8.4f}", end="")
    baseline_s = next(s["Baseline"] for s in SIGNALS if s["Code"] == sig)
    print(f"  (base={baseline_s})")

print("\n" + "="*100)
print("BASELINE (no injection) decay")
print("="*100)
print(f"{'Signal':<8} {'init':>8}", end="")
for t in tick_counts:
    print(f" {'t='+str(t):>8}", end="")
print()
print("-"*100)

for sig in KEY_SIGNALS:
    init_val = next(s["Value"] for s in SIGNALS if s["Code"] == sig)
    print(f"{sig:<8} {init_val:>8.3f}", end="")
    for ticks in tick_counts:
        payload = {"Signals": SIGNALS, "Edges": EDGES, "Gates": GATES, "Ticks": ticks}
        r = requests.post(API, json=payload, timeout=30)
        v = r.json()["finalState"].get(sig, 0)
        print(f" {v:>8.4f}", end="")
    baseline_s = next(s["Baseline"] for s in SIGNALS if s["Code"] == sig)
    print(f"  (base={baseline_s})")

# Now compare SSRI vs baseline at t=1 (the most informative tick)
print("\n" + "="*100)
print("DIFFERENTIAL: SSRI - Baseline at t=1 (what the injection actually changed)")
print("="*100)

base_r = requests.post(API, json={"Signals": SIGNALS, "Edges": EDGES, "Gates": GATES, "Ticks": 1}).json()
ssri_r = requests.post(API, json={"Signals": SIGNALS, "Edges": EDGES, "Gates": GATES, "Inject": SSRI_INJECT, "Ticks": 1}).json()
stress_r = requests.post(API, json={"Signals": SIGNALS, "Edges": EDGES, "Gates": GATES, "Inject": STRESS_INJECT, "Ticks": 1}).json()

print(f"{'Signal':<8} {'baseline':>10} {'ssri':>10} {'Δ ssri':>10} {'stress':>10} {'Δ stress':>10}")
print("-"*60)
for sig in KEY_SIGNALS:
    b = base_r["finalState"].get(sig, 0)
    s = ssri_r["finalState"].get(sig, 0)
    st = stress_r["finalState"].get(sig, 0)
    ds = s - b
    dst = st - b
    marker_s = " ***" if abs(ds) > 0.01 else ""
    marker_st = " ***" if abs(dst) > 0.01 else ""
    print(f"{sig:<8} {b:>10.4f} {s:>10.4f} {ds:>+10.4f}{marker_s} {st:>10.4f} {dst:>+10.4f}{marker_st}")
