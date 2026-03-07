#!/usr/bin/env python3
"""Test vLLM with different prompt/parameter configs to find optimal settings."""
import json, sys, re, time, urllib.request

# Load prompts
PROMPT_DIR = "C:/Users/ailon/repo/MultiAgentAiMcp/Kernel/BioChain.Kernel/Prompts/Data"
with open(f"{PROMPT_DIR}/SIGNALS_ANALYZER_PROMPT.txt", "r", encoding="utf-8") as f:
    SIGNALS_PROMPT = f.read()
with open(f"{PROMPT_DIR}/BIOCHAIN_ANALYZER_PROMPT.txt", "r", encoding="utf-8") as f:
    BIOCHAIN_PROMPT = f.read()

CONDENSED_PROMPT = (
    "You are BioChain-Analyzer. Read behavioral/psychological text and output structured "
    "BioChain protocol lines mapping the neurochemical landscape. Output NOTHING else.\n\n"
    "# TAGS\n"
    "SIGNAL: <formula> — status: <text>\n"
    "RECEPTOR: <formula> — status: <text>\n"
    "GATE: <formula> — status: <text>\n"
    "LIMITER: <formula> — status: <text>\n"
    "TRANSPORT: <formula> — status: <text>\n"
    "INTERFACE: <formula> — status: <text>\n"
    "FORMULA: <formula> — status: <text>\n"
    "FEEDBACK: <formula> — status: <text>\n"
    "DEF: <formula> — status: <text>\n"
    "BIND: <formula> — status: <text>\n"
    "DYSREG: <formula> — status: <text>\n"
    "#PHASE: <name>\n\n"
    "# SIGNAL: TYPE:CODE[state] @REGION\n"
    "Types: NT(DA,5HT,NE,GABA,GLU,ACh) H(CORT,CRH,ACTH,melatonin,T3,insulin,ADH) "
    "P(OXT,BDNF,NPY,substance_P,dynorphin,endorphin,VIP) eCB(2AG,AEA) NI(IL6,TNFa,CRP)\n"
    "States: ↑↑|↑|≈|↓|↓↓|~|⊘|●\n"
    "Regions: VTA,NAc,PFC,AMY,HPC,DRN,LC,HYP,PVN,PIT,mPFC,dlPFC,ACC,INS,OFC,SCN,BG,STR,SN,ADR,PAG,gut\n\n"
    "# RECEPTOR: signal.code(subtype)[state] @REGION  (active|desens|intern|upreg|downreg|resist|primed)\n"
    "# GATE: {SYMBOL(condition) -> effect}  (symbols: ⊨ ⊡ Σ ⊛ ⊳)\n"
    "# LIMITER: CODE[activity] -> reaction  (⧫=rate-limiting)\n"
    "# TRANSPORT: CODE[state] @REGION  (DAT->DA, SERT->5HT, NET->NE)\n"
    "# INTERFACE: SOURCE -> TARGET (pathway)\n"
    "# FORMULA/DEF: signal interactions using -> <- ⊃ ⊣ ⊩ ⇌ ∥\n"
    "# FEEDBACK: ⟳⁻(negative) ⟳⁺(positive)\n"
    "# DYSREG: ⚡.type signal -> effects\n"
    "# BIND: snake_case_name = signal + signal ⊣ signal — status: behavioral composite\n\n"
    "# CRITICAL RULES\n"
    "1. One tag per line. Every line needs ' — status: ' suffix.\n"
    "2. Always include TYPE prefix (NT:, H:, P:, eCB:, NI:).\n"
    "3. ALL operands MUST be real neurochemical codes (DA, 5HT, NE, GABA, GLU, CORT, BDNF, OXT etc). "
    "NEVER use abstractions (mood, attention, cognition, behavior, emotion, reward, impulse, focus, energy). "
    "Use BIND for behavioral outcomes.\n"
    "4. Single @REGION per SIGNAL. Use INTERFACE for pathways.\n"
    "5. Every SIGNAL must appear in at least one FORMULA/FEEDBACK/DEF/DYSREG.\n"
    "6. Aim for 15-35 lines.\n\n"
    "# EXAMPLE\n"
    "Input: 'Persistent low mood, anhedonia, sleep disruption.'\n"
    "Output:\n"
    "SIGNAL: NT:5HT[↓] @DRN — status: serotonergic deficit\n"
    "SIGNAL: NT:DA[↓] @VTA — status: anhedonia\n"
    "SIGNAL: H:CORT[↑↑] @ADR — status: sustained cortisol\n"
    "RECEPTOR: DA.D2(Gi)[.desens] @NAc — status: reward receptor downregulation\n"
    "LIMITER: TPH2[↓] -> 5HT.synthesis @DRN — status: serotonin production impaired\n"
    "TRANSPORT: SERT[active] @DRN — status: normal clearance worsening low 5HT\n"
    "INTERFACE: VTA -> NAc (mesolimbic) — status: reward pathway hypoactive\n"
    "FORMULA: CORT[↑↑]@ADR -> BDNF[↓]@HPC — status: cortisol suppresses neuroplasticity\n"
    "FORMULA: 5HT[↓]@DRN -> DA[↓]@VTA — status: serotonin deficit reduces dopamine\n"
    "FEEDBACK: ⟳⁺ CORT[↑↑]@ADR -> CRH[↑]@PVN — status: stress amplification\n"
    "BIND: mood_regulation = 5HT@DRN + DA@VTA + GABA@AMY — status: impaired\n"
    "DYSREG: ⚡.sustained CORT[↑↑]@ADR -> BDNF[↓]@HPC — status: neurotoxicity risk\n"
)

QUESTION = (
    "Analyze this questionnaire answer about the person:\n"
    "Question: When you feel overwhelmed or stressed, how do you typically cope?\n"
    "Answer: I tend to shut down and isolate myself. Sometimes I overeat or stay up "
    "too late scrolling my phone. I know its not healthy but in the moment it feels "
    "like the only option."
)

# Allowed signal codes
VALID_CODES = {
    "DA", "5HT", "NE", "GABA", "GLU", "ACh", "glycine", "histamine", "adenosine",
    "CORT", "cortisone", "ACTH", "CRH", "TRH", "TSH", "T3", "T4", "melatonin",
    "insulin", "glucagon", "leptin", "ghrelin", "prolactin", "GH", "IGF1", "ADH",
    "estradiol", "progesterone", "testosterone", "DHEA", "aldosterone", "GnRH",
    "OXT", "AVP", "BDNF", "NGF", "NPY", "substance_P", "VIP", "CCK", "orexin",
    "galanin", "dynorphin", "enkephalin", "endorphin", "CGRP", "neurotensin",
    "somatostatin", "2AG", "AEA", "PEA", "OEA",
    "IL1b", "IL6", "IL10", "TNFa", "IFNg", "CRP", "TGFb",
    "allopregnanolone", "THDOC", "pregnenolone", "DHEAS",
    "ADRENALINE", "adrenaline", "glucose",
}
VALID_TYPES = {"NT", "H", "P", "eCB", "NI", "NS"}


def strip_think(text):
    """Remove <think>...</think> blocks from vLLM output."""
    while "<think>" in text and "</think>" in text:
        start = text.index("<think>")
        end = text.index("</think>") + len("</think>")
        text = (text[:start] + text[end:]).strip()
    if text.startswith("</think>"):
        text = text[len("</think>"):].strip()
    return text


def score_output(text):
    """Score LLM output quality. Higher = better."""
    text = strip_think(text)
    lines = [l.strip() for l in text.strip().split("\n") if l.strip()]
    total = len(lines)
    if total == 0:
        return {"total": 0, "valid_tags": 0, "valid_types": 0, "valid_codes": 0,
                "has_status": 0, "abstract_violations": 0, "binds": 0, "score": 0}

    valid_tags = 0
    valid_types = 0
    valid_codes = 0
    has_status = 0
    abstract_violations = 0
    binds = 0

    known_tags = {"SIGNAL", "RECEPTOR", "GATE", "LIMITER", "TRANSPORT", "INTERFACE",
                  "FORMULA", "FEEDBACK", "DEF", "BIND", "DYSREG", "STATE", "#PHASE",
                  "FAIL", "CONSTRAINT", "EQUILIBRIUM", "BOUNDARY", "CONSERVE",
                  "TOOL", "LLM_GATE", "EMIT", "MESSAGE", "MODULE", "IMPORT"}

    abstract_words = {"mood", "attention", "cognition", "behavior", "emotion", "reward",
                      "impulse", "focus", "energy", "motivation", "anxiety", "fear",
                      "stress", "memory", "learning", "sleep", "appetite", "arousal",
                      "pleasure", "pain", "consciousness", "alertness", "performance",
                      "curiosity", "engagement", "seek", "isolation", "shutdown",
                      "compulsion", "obligation", "routine", "logic", "imagination"}

    for line in lines:
        tag_match = re.match(r'^(#?[A-Z_]+):', line)
        if tag_match and tag_match.group(1) in known_tags:
            valid_tags += 1

        if " — status: " in line or " -- status: " in line:
            has_status += 1

        if line.startswith("SIGNAL:"):
            type_match = re.search(r'(NT|H|P|eCB|NI|NS):', line)
            if type_match:
                valid_types += 1
            code_match = re.search(r'(?:NT|H|P|eCB|NI|NS):(\w+)\[', line)
            if code_match and code_match.group(1) in VALID_CODES:
                valid_codes += 1
            formula = line.split(":", 1)[1].split(" — ")[0] if " — " in line else line
            for word in abstract_words:
                if re.search(r'\b' + word + r'\b', formula, re.IGNORECASE):
                    abstract_violations += 1
                    break

        if line.startswith(("FORMULA:", "FEEDBACK:", "DEF:", "DYSREG:")):
            formula = line.split(":", 1)[1].split(" — ")[0] if " — " in line else line
            for word in abstract_words:
                if re.search(r'\b' + word + r'\b', formula, re.IGNORECASE):
                    abstract_violations += 1
                    break

        if line.startswith("BIND:"):
            binds += 1

    signal_lines = sum(1 for l in lines if l.startswith("SIGNAL:"))
    tag_pct = valid_tags / total
    status_pct = has_status / total
    type_pct = valid_types / max(signal_lines, 1)
    code_pct = valid_codes / max(signal_lines, 1)
    abstract_penalty = abstract_violations / total
    bind_bonus = min(binds / 3, 1.0)
    line_bonus = 1.0 if 15 <= total <= 35 else 0.7 if 10 <= total <= 40 else 0.4

    score = (
        tag_pct * 20 +
        status_pct * 15 +
        type_pct * 20 +
        code_pct * 20 +
        (1 - abstract_penalty) * 15 +
        bind_bonus * 5 +
        line_bonus * 5
    )

    return {
        "total": total,
        "valid_tags": valid_tags,
        "valid_types": valid_types,
        "valid_codes": valid_codes,
        "has_status": has_status,
        "abstract_violations": abstract_violations,
        "binds": binds,
        "score": round(score, 1),
    }


def run_test(prompt, temp, pp):
    """Run a single vLLM test via urllib (no curl dependency)."""
    req = {
        "model": "/models/Qwen3.5-A3B",
        "messages": [
            {"role": "system", "content": prompt},
            {"role": "user", "content": QUESTION}
        ],
        "max_tokens": 2048,
        "temperature": temp,
        "top_p": 0.8,
        "top_k": 20,
        "presence_penalty": pp,
    }

    data = json.dumps(req).encode("utf-8")
    request = urllib.request.Request(
        "http://localhost:8000/v1/chat/completions",
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=120) as resp:
            body = json.loads(resp.read().decode("utf-8"))
        content = body["choices"][0]["message"]["content"]
        tokens = body.get("usage", {}).get("completion_tokens", 0)
        return strip_think(content), tokens
    except Exception as e:
        return f"ERROR: {e}", 0


# Test matrix
prompts = {
    "signals": (SIGNALS_PROMPT, len(SIGNALS_PROMPT)),
    "biochain": (BIOCHAIN_PROMPT, len(BIOCHAIN_PROMPT)),
    "condensed": (CONDENSED_PROMPT, len(CONDENSED_PROMPT)),
}
temps = [0.3, 0.7]
penalties = [0.0, 0.8, 1.5]

print("=" * 80)
print("vLLM CONFIG TEST MATRIX — Qwen3.5-A3B")
print("=" * 80)
for name, (_, size) in prompts.items():
    print(f"  {name}: {size} chars")
print(f"  Temps: {temps}")
print(f"  Presence penalties: {penalties}")
print(f"  Total tests: {len(prompts) * len(temps) * len(penalties)}")
print("=" * 80)

results = []
outputs = {}
for pname, (prompt, psize) in prompts.items():
    for temp in temps:
        for pp in penalties:
            test_id = f"{pname}_t{temp}_pp{pp}"
            sys.stdout.write(f"  Testing {test_id}... ")
            sys.stdout.flush()

            t0 = time.time()
            content, tokens = run_test(prompt, temp, pp)
            elapsed = time.time() - t0
            scores = score_output(content)
            outputs[test_id] = content

            results.append({
                "id": test_id,
                "prompt": pname,
                "prompt_size": psize,
                "temp": temp,
                "pp": pp,
                "tokens": tokens,
                "elapsed": round(elapsed, 1),
                **scores,
            })

            signal_count = sum(1 for l in content.split("\n") if l.strip().startswith("SIGNAL:"))
            print(f"score={scores['score']:.1f} lines={scores['total']} "
                  f"types={scores['valid_types']}/{signal_count} "
                  f"codes={scores['valid_codes']}/{signal_count} "
                  f"abstracts={scores['abstract_violations']} binds={scores['binds']} "
                  f"tokens={tokens} time={elapsed:.1f}s")

# Sort by score
results.sort(key=lambda x: x["score"], reverse=True)

print("\n" + "=" * 80)
print("RANKED RESULTS")
print("=" * 80)
print(f"{'Rank':<5} {'Config':<30} {'Score':<7} {'Lines':<6} {'Tags':<5} {'Types':<6} "
      f"{'Codes':<6} {'Status':<7} {'Abs.V':<6} {'BINDs':<6} {'Tok':<6} {'Time':<6}")
print("-" * 100)
for i, r in enumerate(results):
    print(f"{i+1:<5} {r['id']:<30} {r['score']:<7.1f} {r['total']:<6} {r['valid_tags']:<5} "
          f"{r['valid_types']:<6} {r['valid_codes']:<6} {r['has_status']:<7} "
          f"{r['abstract_violations']:<6} {r['binds']:<6} {r['tokens']:<6} {r['elapsed']:<6.1f}")

# Print best config
best = results[0]
print(f"\nBEST: {best['id']} (score={best['score']:.1f})")
print(f"  Prompt: {best['prompt']} ({best['prompt_size']} chars)")
print(f"  Temperature: {best['temp']}, Presence Penalty: {best['pp']}")

# Save full results
with open("C:/tmp/vllm_test_results.json", "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2)

# Print top 3 outputs
for i in range(min(3, len(results))):
    rid = results[i]["id"]
    print(f"\n{'='*60}")
    print(f"#{i+1} OUTPUT: {rid} (score={results[i]['score']:.1f})")
    print(f"{'='*60}")
    out = outputs.get(rid, "")
    # Show first 40 lines
    out_lines = out.strip().split("\n")
    for line in out_lines[:40]:
        print(line)
    if len(out_lines) > 40:
        print(f"... ({len(out_lines) - 40} more lines)")
