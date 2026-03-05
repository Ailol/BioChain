"""4-way benchmark: fat vs lean system prompt × 3B vs 8B"""
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

MODELS = [
    {"name": "biochain-engine:latest",        "label": "8B fat"},
    {"name": "engine-lean:latest",             "label": "8B lean"},
    {"name": "nanbeige-biochain-api:latest",   "label": "3B fat"},
    {"name": "nanbeige-api-lean:latest",       "label": "3B lean"},
]

TAG_RE = re.compile(r'^(SIGNAL|RECEPTOR|GATE|LIMITER|TRANSPORT|INTERFACE|FORMULA|FEEDBACK|DEF|DYSREG|HYPOTHESIS|PREDICTION|INTERVENTION|STATE):', re.MULTILINE)
PHASE_RE = re.compile(r'^#PHASE:', re.MULTILINE)

def run(model_name, prompt_text):
    start = time.time()
    resp = requests.post(OLLAMA, json={
        "model": model_name, "stream": False,
        "options": {"num_predict": 2048},
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

    # check for formatting quirks
    double_phase = content.count("#PHASE: #PHASE:")

    return {
        "tokens": tokens, "time": dur, "wall": wall,
        "speed": tokens / max(dur, 0.1),
        "think_w": think_w, "lines": len(lines),
        "tags": len(tags), "phases": len(phases),
        "double_phase": double_phase, "content": content,
    }

# Warm up
print("Warming up 4 models...")
for m in MODELS:
    requests.post(OLLAMA, json={
        "model": m["name"], "stream": False,
        "options": {"num_predict": 10},
        "messages": [{"role": "user", "content": "test"}]
    }, timeout=60)
    print(f"  {m['label']} ready")

print("\n" + "="*90)
print("BENCHMARK: fat vs lean system prompt × 3B vs 8B")
print("="*90)

results = {m["label"]: [] for m in MODELS}

for prompt in PROMPTS:
    print(f"\n--- {prompt['label']} ---")
    for model in MODELS:
        r = run(model["name"], prompt["text"])
        results[model["label"]].append(r)
        extras = []
        if r["think_w"] > 0: extras.append(f"think={r['think_w']}w")
        if r["double_phase"] > 0: extras.append(f"dupe_phase={r['double_phase']}")
        extra = f"  ({', '.join(extras)})" if extras else ""
        print(f"  {model['label']:10s}  {r['wall']:5.1f}s  {r['tokens']:4d}tok  {r['speed']:5.1f}t/s  {r['lines']:3d}ln  {r['tags']:3d}tags  {r['phases']:2d}ph{extra}")

# Summary
print("\n" + "="*90)
print(f"{'Model':10s}  {'Avg time':>8s}  {'Speed':>8s}  {'Tags':>5s}  {'Lines':>5s}  {'Phases':>6s}  {'Dupe#PH':>7s}  {'Think':>6s}")
print("-"*90)
for label, runs in results.items():
    avg_wall = sum(r["wall"] for r in runs) / len(runs)
    avg_speed = sum(r["speed"] for r in runs) / len(runs)
    total_tags = sum(r["tags"] for r in runs)
    total_lines = sum(r["lines"] for r in runs)
    total_phases = sum(r["phases"] for r in runs)
    total_dupe = sum(r["double_phase"] for r in runs)
    total_think = sum(r["think_w"] for r in runs)
    print(f"  {label:10s}  {avg_wall:6.1f}s  {avg_speed:6.1f}t/s  {total_tags:5d}  {total_lines:5d}  {total_phases:6d}  {total_dupe:7d}  {total_think:6d}")

# Sample Q1 from each
print("\n" + "="*90)
print("SAMPLE: Q1 first 400 chars")
print("="*90)
for model in MODELS:
    r = results[model["label"]][0]
    print(f"\n--- {model['label']} ({r['tags']} tags, {r['wall']:.1f}s) ---")
    print(r["content"][:400])
