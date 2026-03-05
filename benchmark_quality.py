"""Quality check: full output from top 3B mid configs + 8B fat reference for Q1."""
import requests, json, time, re

OLLAMA = "http://localhost:11434/api/chat"

PROMPT = "Analyze this psychological assessment:\n\nPsychological assessment question: What gets you moving in the morning?\n\nSELECTED: A goal, a plan, the pull of something I want — I wake up driven\nREJECTED: Connection — hearing from someone, being needed, feeling part of something\nREJECTED: Routine and safety — knowing what to expect grounds me\nREJECTED: Curiosity or novelty — new things to explore, nothing repeated"

MODELS = [
    {"name": "biochain-engine:latest",   "label": "8B fat",        "options": {"num_predict": 2048}},
    {"name": "nanbeige-api-mid:latest",  "label": "3B mid mp0.05", "options": {"num_predict": 2048, "temperature": 0.2, "top_k": 0, "min_p": 0.05}},
    {"name": "nanbeige-api-mid:latest",  "label": "3B mid tk20",   "options": {"num_predict": 2048, "temperature": 0.2, "top_k": 20, "min_p": 0.1}},
    {"name": "nanbeige-biochain-api:latest", "label": "3B fat",    "options": {"num_predict": 2048}},
]

TAG_RE = re.compile(r'^(SIGNAL|RECEPTOR|GATE|LIMITER|TRANSPORT|INTERFACE|FORMULA|FEEDBACK|DEF|DYSREG|HYPOTHESIS|PREDICTION|INTERVENTION|STATE):', re.MULTILINE)
PHASE_RE = re.compile(r'^#PHASE:', re.MULTILINE)

# Warm up
print("Warming up...")
for m in {m["name"] for m in MODELS}:
    requests.post(OLLAMA, json={"model": m, "stream": False, "options": {"num_predict": 10},
        "messages": [{"role": "user", "content": "test"}]}, timeout=60)
print("Ready\n")

for model in MODELS:
    start = time.time()
    resp = requests.post(OLLAMA, json={
        "model": model["name"], "stream": False,
        "options": model["options"],
        "messages": [{"role": "user", "content": PROMPT}]
    }, timeout=300)
    wall = time.time() - start
    d = resp.json()
    msg = d.get("message", {}).get("content", "")
    tokens = d.get("eval_count", 0)

    te = msg.find("</think>")
    content = msg[te+8:].strip() if te > 0 else msg.strip()

    tags = TAG_RE.findall(content)
    phases = PHASE_RE.findall(content)
    double = content.count("#PHASE: #PHASE:")

    tag_counts = {}
    for t in tags:
        tag_counts[t] = tag_counts.get(t, 0) + 1

    print("=" * 100)
    print(f"{model['label']}  |  {wall:.1f}s  {tokens} tok  {len(tags)} tags  {len(phases)} phases  dupe={double}")
    print(f"Tag distribution: {dict(sorted(tag_counts.items(), key=lambda x: -x[1]))}")
    print("=" * 100)
    print(content)
    print()
