"""Benchmark: 3B fat with parameter variations to fix #PHASE: #PHASE: duplication.
Goal: keep the good biochemical reasoning, eliminate formatting bugs."""
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

MODEL = "nanbeige-biochain-api:latest"

CONFIGS = [
    # baseline (previous benchmark reference)
    {"label": "baseline",       "options": {}},
    # repeat penalty — directly penalizes #PHASE: #PHASE: repetition
    {"label": "rp1.1",          "options": {"repeat_penalty": 1.1}},
    {"label": "rp1.2",          "options": {"repeat_penalty": 1.2}},
    {"label": "rp1.3",          "options": {"repeat_penalty": 1.3}},
    {"label": "rp1.5",          "options": {"repeat_penalty": 1.5}},
    # temp + repeat penalty combos
    {"label": "t0.1+rp1.2",    "options": {"temperature": 0.1, "repeat_penalty": 1.2}},
    {"label": "t0.3+rp1.2",    "options": {"temperature": 0.3, "repeat_penalty": 1.2}},
    # top_k to constrain token selection
    {"label": "tk10+rp1.2",    "options": {"top_k": 10, "repeat_penalty": 1.2}},
    {"label": "tk20+rp1.2",    "options": {"top_k": 20, "repeat_penalty": 1.2}},
    # frequency/presence penalty (ollama supports these)
    {"label": "fp0.5",         "options": {"frequency_penalty": 0.5}},
    {"label": "fp1.0",         "options": {"frequency_penalty": 1.0}},
    # min_p combos
    {"label": "mp0.05+rp1.2",  "options": {"min_p": 0.05, "repeat_penalty": 1.2}},
]

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

    te = msg.find("</think>")
    content = msg[te+8:].strip() if te > 0 else msg.strip()
    think_w = len(msg[:te].split()) if te > 0 else 0

    tags = TAG_RE.findall(content)
    phases = PHASE_RE.findall(content)
    lines = [l for l in content.split("\n") if l.strip()]
    double_phase = content.count("#PHASE: #PHASE:")

    # count unique tag types
    tag_types = set(tags)

    # check for repetition loops (same line appearing 3+ times)
    line_counts = {}
    for l in lines:
        ls = l.strip()
        if ls:
            line_counts[ls] = line_counts.get(ls, 0) + 1
    repeat_lines = sum(1 for c in line_counts.values() if c >= 3)

    return {
        "tokens": tokens, "time": dur, "wall": wall,
        "speed": tokens / max(dur, 0.1),
        "think_w": think_w, "lines": len(lines),
        "tags": len(tags), "tag_types": len(tag_types),
        "phases": len(phases), "double_phase": double_phase,
        "repeat_lines": repeat_lines, "content": content,
    }

# Warm up
print("Warming up nanbeige-biochain-api...")
requests.post(OLLAMA, json={
    "model": MODEL, "stream": False, "options": {"num_predict": 10},
    "messages": [{"role": "user", "content": "test"}]
}, timeout=60)
print("Ready\n")

print("=" * 110)
print("BENCHMARK: 3B fat parameter sweep — fix #PHASE: #PHASE: dupe while keeping quality")
print("=" * 110)

results = {c["label"]: [] for c in CONFIGS}

for prompt in PROMPTS:
    print(f"\n--- {prompt['label']} ---")
    for cfg in CONFIGS:
        r = run(prompt["text"], cfg["options"])
        results[cfg["label"]].append(r)
        flags = []
        if r["think_w"] > 0: flags.append(f"think={r['think_w']}w")
        if r["double_phase"] > 0: flags.append(f"DUPE={r['double_phase']}")
        if r["repeat_lines"] > 0: flags.append(f"loops={r['repeat_lines']}")
        flag = f"  ({', '.join(flags)})" if flags else ""
        print(f"  {cfg['label']:15s}  {r['wall']:5.1f}s  {r['tokens']:4d}tok  {r['speed']:5.1f}t/s  {r['lines']:3d}ln  {r['tags']:3d}tags({r['tag_types']:2d}types)  {r['phases']:2d}ph{flag}")

# Summary
print("\n" + "=" * 110)
print(f"{'Config':15s}  {'Time':>6s}  {'Speed':>8s}  {'Tags':>5s}  {'Types':>5s}  {'Lines':>5s}  {'Dupes':>5s}  {'Loops':>5s}  {'Think':>5s}")
print("-" * 110)
print(f"  {'8B fat (ref)':15s}  {'29.2s':>6s}  {'49.3t/s':>8s}  {'131':>5s}  {'10':>5s}  {'134':>5s}  {'0':>5s}  {'0':>5s}  {'0':>5s}")
print("-" * 110)
for label, runs in results.items():
    avg_wall = sum(r["wall"] for r in runs) / len(runs)
    avg_speed = sum(r["speed"] for r in runs) / len(runs)
    total_tags = sum(r["tags"] for r in runs)
    max_types = max(r["tag_types"] for r in runs)
    total_lines = sum(r["lines"] for r in runs)
    total_dupe = sum(r["double_phase"] for r in runs)
    total_loops = sum(r["repeat_lines"] for r in runs)
    total_think = sum(r["think_w"] for r in runs)
    marker = " <-- CLEAN" if total_dupe == 0 and total_loops == 0 else ""
    print(f"  {label:15s}  {avg_wall:5.1f}s  {avg_speed:6.1f}t/s  {total_tags:5d}  {max_types:5d}  {total_lines:5d}  {total_dupe:5d}  {total_loops:5d}  {total_think:5d}{marker}")

# Show Q1 from best clean config (most tags with 0 dupes)
clean = {l: r for l, r in results.items() if sum(x["double_phase"] for x in r) == 0}
if clean:
    best = max(clean.keys(), key=lambda k: sum(r["tags"] for r in clean[k]))
    print(f"\n{'=' * 110}")
    print(f"BEST CLEAN CONFIG: {best} — Q1 full output")
    print("=" * 110)
    r = results[best][0]
    print(f"({r['tags']} tags, {r['tag_types']} types, {r['wall']:.1f}s, {r['tokens']} tok)")
    print(r["content"])
else:
    # Show least dupes
    best = min(results.keys(), key=lambda k: sum(x["double_phase"] for x in results[k]))
    print(f"\n{'=' * 110}")
    print(f"LEAST DUPES CONFIG: {best} — Q1 full output")
    print("=" * 110)
    r = results[best][0]
    dupes = sum(x["double_phase"] for x in results[best])
    print(f"({r['tags']} tags, {r['tag_types']} types, {r['wall']:.1f}s, dupes={dupes})")
    print(r["content"])
