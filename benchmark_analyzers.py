"""Benchmark: nanbeige-biochain-api (3B Q6_K) vs biochain-engine (8B Q6_K)"""
import requests, json, time, re, sys

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
    {"name": "biochain-engine:latest",       "label": "8B Q6_K (engine)", "options": {"num_predict": 2048}},
    {"name": "nanbeige-biochain-api:latest",  "label": "3B Q6_K (nanbeige)", "options": {"num_predict": 2048}},
]

TAG_PATTERN = re.compile(r'^(SIGNAL|RECEPTOR|GATE|LIMITER|TRANSPORT|INTERFACE|FORMULA|FEEDBACK|DEF|DYSREG|HYPOTHESIS|PREDICTION|INTERVENTION|STATE):', re.MULTILINE)
PHASE_PATTERN = re.compile(r'^#PHASE:', re.MULTILINE)

def analyze(model_name, prompt_text, options):
    start = time.time()
    resp = requests.post(OLLAMA, json={
        "model": model_name,
        "stream": False,
        "options": options,
        "messages": [{"role": "user", "content": prompt_text}]
    }, timeout=300)
    elapsed = time.time() - start
    d = resp.json()
    msg = d.get("message", {}).get("content", "")
    tokens = d.get("eval_count", 0)
    eval_dur = d.get("eval_duration", 0) / 1e9

    # Strip thinking
    think_end = msg.find("</think>")
    if think_end > 0:
        think_part = msg[:think_end]
        content = msg[think_end+8:].strip()
        think_words = len(think_part.split())
    else:
        content = msg.strip()
        think_words = 0

    # Count tags
    tags = TAG_PATTERN.findall(content)
    phases = PHASE_PATTERN.findall(content)
    lines = [l for l in content.split("\n") if l.strip()]

    return {
        "tokens": tokens,
        "time": eval_dur,
        "wall": elapsed,
        "speed": tokens / max(eval_dur, 0.1),
        "think_words": think_words,
        "lines": len(lines),
        "tags": len(tags),
        "phases": len(phases),
        "tag_dist": {},
        "content": content,
    }

# Warm up both models
print("Warming up models...")
for m in MODELS:
    requests.post(OLLAMA, json={
        "model": m["name"], "stream": False,
        "options": {"num_predict": 10},
        "messages": [{"role": "user", "content": "test"}]
    }, timeout=60)
    print(f"  {m['label']} loaded")

print("\n" + "="*80)
print("BENCHMARK: nanbeige 3B Q6_K vs Qwen 8B Q6_K as BioChain analyzer")
print("="*80)

results = {m["label"]: [] for m in MODELS}

for prompt in PROMPTS:
    print(f"\n--- {prompt['label']} ---")
    for model in MODELS:
        r = analyze(model["name"], prompt["text"], model["options"])
        results[model["label"]].append(r)
        think_str = f" (think={r['think_words']}w)" if r["think_words"] > 0 else ""
        print(f"  {model['label']:25s}  {r['wall']:5.1f}s  {r['tokens']:4d}tok  {r['speed']:5.1f}t/s  {r['lines']:3d}lines  {r['tags']:3d}tags  {r['phases']:2d}ph{think_str}")

# Summary
print("\n" + "="*80)
print("SUMMARY")
print("="*80)
for label, runs in results.items():
    avg_wall = sum(r["wall"] for r in runs) / len(runs)
    avg_speed = sum(r["speed"] for r in runs) / len(runs)
    total_tags = sum(r["tags"] for r in runs)
    total_lines = sum(r["lines"] for r in runs)
    total_think = sum(r["think_words"] for r in runs)
    print(f"  {label:25s}  avg={avg_wall:5.1f}s  {avg_speed:5.1f}t/s  tags={total_tags:3d}  lines={total_lines:3d}  think={total_think}w")

# Show sample output from each for Q1
print("\n" + "="*80)
print("SAMPLE OUTPUT: Q1 Morning drive")
print("="*80)
for model in MODELS:
    idx = 0
    r = results[model["label"]][idx]
    print(f"\n--- {model['label']} ({r['tokens']} tokens, {r['wall']:.1f}s) ---")
    # Show first 800 chars
    print(r["content"][:800])
    if len(r["content"]) > 800:
        print("... [truncated]")
