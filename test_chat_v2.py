"""Test biochain-chat-v2: hidden BioChain reasoning + natural language response."""
import requests, time

OLLAMA = "http://localhost:11434/api/chat"
MODEL = "biochain-chat-v2:latest"

PROMPTS = [
    "What gets you moving in the morning? I wake up driven — a goal, a plan, the pull of something I want.",
    "My ex texted me last night and I couldn't sleep. My stomach's been in knots all morning.",
    "I got promoted today but honestly I feel nothing. Shouldn't I be happy?",
    "Every time I try to relax I end up doom-scrolling for hours. Why can't I just stop?",
]

# Warm up
print("Warming up...")
requests.post(OLLAMA, json={"model": MODEL, "stream": False,
    "options": {"num_predict": 10},
    "messages": [{"role": "user", "content": "test"}]}, timeout=60)
print("Ready\n")

for i, prompt in enumerate(PROMPTS):
    print("=" * 90)
    print(f"PROMPT {i+1}: {prompt}")
    print("=" * 90)

    start = time.time()
    resp = requests.post(OLLAMA, json={
        "model": MODEL, "stream": False,
        "options": {"num_predict": 4096},
        "messages": [{"role": "user", "content": prompt}]
    }, timeout=300)
    wall = time.time() - start
    d = resp.json()
    msg = d.get("message", {}).get("content", "")
    tokens = d.get("eval_count", 0)

    te = msg.find("</think>")
    if te > 0:
        think = msg[:te].strip()
        response = msg[te+8:].strip()
        think_words = len(think.split())
    else:
        think = ""
        response = msg.strip()
        think_words = 0

    print(f"\n[{wall:.1f}s | {tokens} tok | think: {think_words}w]")
    print(f"\nTHINKING (first 600 chars):")
    print(think[:600])
    if len(think) > 600:
        print("... [truncated]")
    print(f"\nRESPONSE:")
    print(response)
    print()
