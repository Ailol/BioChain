"""Benchmark: 3B mid-weight system prompt with parameter variations.
Reuses 8B fat / 3B fat / 3B lean results from benchmark_lean.py."""
import requests, json, time, re

OLLAMA = "http://localhost:11434/api/chat"

PROMPTS = [
    {
        "label": "Q1: Morning drive",
        "text": "Analyze this psychological assessment:\n\nPsychological assessment question: What gets you moving in the morning?\n\nSELECTED: A goal, a plan, the pull of something I want — I wake up driven\nREJECTED: Connection — hearing from someone, being needed, feeling part of something\nREJECTED: Routine and safety — knowing what to expect grounds me\nREJECTED: Curiosity or novelty — new things to explore, nothing repeated"
    },
    {
        "label": "Q2: Energy pattern",
        "text": "Analyze this psychological assessment:\n\nPsychological assessment question: Your energy pattern across a typical day:\n\nSELECTED: Steady, predictable — I maintain even energy throughout\nREJECTED: Burst-crash — intense mornings then afternoon collapse\nREJECTED: Slow ramp — low mornings, peak evenings\nREJECTED: Erratic — energy depends on what interests me, not the clock"
    },
    {
        "label": "Q3: Bad news reaction",
        "text": "Analyze this psychological assessment:\n\nPsychological assessment question: Your body's first reaction to sudden bad news:\n\nSELECTED: Gut drop, nausea, stomach tightens immediately\nREJECTED: Heart races, flushed, restless energy needs outlet\nREJECTED: Freeze — blank, numb, disconnected from body\nREJECTED: Tears come fast, throat tightens, chest aches"
    },
]

# All variants use nanbeige-api-mid as base, override options per-run
CONFIGS = [
    {"label": "3B mid",           "options": {"temperature": 0.2, "top_k": 0, "min_p": 0.1}},
    {"label": "3B mid t0.4",      "options": {"temperature": 0.4, "top_k": 0, "min_p": 0.1}},
    {"label": "3B mid t0.1",      "options": {"temperature": 0.1, "top_k": 0, "min_p": 0.1}},
    {"label": "3B mid mp0.05",    "options": {"temperature": 0.2, "top_k": 0, "min_p": 0.05}},
    {"label": "3B mid mp0.2",     "options": {"temperature": 0.2, "top_k": 0, "min_p": 0.2}},
    {"label": "3B mid rp1.15",    "options": {"temperature": 0.2, "top_k": 0, "min_p": 0.1, "repeat_penalty": 1.15}},
    {"label": "3B mid tk20",      "options": {"temperature": 0.2, "top_k": 20, "min_p": 0.1}},
]

MODEL = "nanbeige-api-mid:latest"

TAG_RE = re.compile(r'^(SIGNAL|RECEPTOR|GATE|LIMITER|TRANSPORT|INTERFACE|FORMULA|FEEDBACK|DEF|DYSREG|HYPOTHESIS|PREDICTION|INTERVENTION|STATE):', re.MULTILINE)
PHASE_RE = re.compile(r'^#PHASE:', re.MULTILINE)

def run(prompt_text, options):
    start = time.time()
    resp = requests.post(OLLAMA, json={
        "model": MODEL, "stream": False,
        "options": {"num_predict": 2048, **options},
        "messages": [{"role": "user", "content": prompt_text}]
    }, timeout=300)
    wall = time.time() - start
    d = resp.json()
    msg = d.get("message", {}).get("content", "")
    tokens = d.get("eval_count", 0)
    dur = d.get("eval_duration", 0) / 1e9

    # strip thinking
    te = msg.find("</think>")
    content = msg[te+8:].strip() if te > 0 else msg.strip()
    think_w = len(msg[:te].split()) if te > 0 else 0

    tags = TAG_RE.findall(content)
    phases = PHASE_RE.findall(content)
    lines = [l for l in content.split("\n") if l.strip()]
    double_phase = content.count("#PHASE: #PHASE:")

    return {
        "tokens": tokens, "time": dur, "wall": wall,
        "speed": tokens / max(dur, 0.1),
        "think_w": think_w, "lines": len(lines),
        "tags": len(tags), "phases": len(phases),
        "double_phase": double_phase, "content": content,
    }

# Warm up
print("Warming up nanbeige-api-mid...")
requests.post(OLLAMA, json={
    "model": MODEL, "stream": False,
    "options": {"num_predict": 10},
    "messages": [{"role": "user", "content": "test"}]
}, timeout=60)
print("  ready\n")

print("=" * 100)
print("BENCHMARK: 3B mid-weight system prompt — parameter sweep")
print("=" * 100)

# Previous results for comparison
print("\n--- Reference from previous benchmark ---")
print(f"  {'8B fat':15s}  avg 29.2s   49.3t/s  131 tags  134 lines  0 dupe")
print(f"  {'3B fat':15s}  avg 23.9s  165.7t/s  109 tags  126 lines  17 dupe")
print(f"  {'3B lean':15s}  avg 21.5s  181.0t/s   11 tags   95 lines  0 dupe")

results = {c["label"]: [] for c in CONFIGS}

for prompt in PROMPTS:
    print(f"\n--- {prompt['label']} ---")
    for cfg in CONFIGS:
        r = run(prompt["text"], cfg["options"])
        results[cfg["label"]].append(r)
        extras = []
        if r["think_w"] > 0: extras.append(f"think={r['think_w']}w")
        if r["double_phase"] > 0: extras.append(f"dupe={r['double_phase']}")
        extra = f"  ({', '.join(extras)})" if extras else ""
        print(f"  {cfg['label']:15s}  {r['wall']:5.1f}s  {r['tokens']:4d}tok  {r['speed']:5.1f}t/s  {r['lines']:3d}ln  {r['tags']:3d}tags  {r['phases']:2d}ph{extra}")

# Summary
print("\n" + "=" * 100)
print(f"{'Config':15s}  {'Avg time':>8s}  {'Speed':>8s}  {'Tags':>5s}  {'Lines':>5s}  {'Phases':>6s}  {'Dupes':>5s}  {'Think':>6s}")
print("-" * 100)
print(f"  {'8B fat (ref)':15s}  {'29.2s':>8s}  {'49.3t/s':>8s}  {'131':>5s}  {'134':>5s}  {'3':>6s}  {'0':>5s}  {'0':>6s}")
print(f"  {'3B fat (ref)':15s}  {'23.9s':>8s}  {'165.7t/s':>8s}  {'109':>5s}  {'126':>5s}  {'17':>6s}  {'17':>5s}  {'0':>6s}")
print(f"  {'3B lean (ref)':15s}  {'21.5s':>8s}  {'181.0t/s':>8s}  {'11':>5s}  {'95':>5s}  {'8':>6s}  {'0':>5s}  {'0':>6s}")
print("-" * 100)
for label, runs in results.items():
    avg_wall = sum(r["wall"] for r in runs) / len(runs)
    avg_speed = sum(r["speed"] for r in runs) / len(runs)
    total_tags = sum(r["tags"] for r in runs)
    total_lines = sum(r["lines"] for r in runs)
    total_phases = sum(r["phases"] for r in runs)
    total_dupe = sum(r["double_phase"] for r in runs)
    total_think = sum(r["think_w"] for r in runs)
    print(f"  {label:15s}  {avg_wall:6.1f}s  {avg_speed:6.1f}t/s  {total_tags:5d}  {total_lines:5d}  {total_phases:6d}  {total_dupe:5d}  {total_think:6d}")

# Sample Q1 from best config (most tags)
best_label = max(results.keys(), key=lambda k: sum(r["tags"] for r in results[k]))
print(f"\n{'=' * 100}")
print(f"SAMPLE: Q1 first 500 chars — {best_label}")
print("=" * 100)
r = results[best_label][0]
print(f"({r['tags']} tags, {r['wall']:.1f}s, {r['tokens']} tok)")
print(r["content"][:500])
