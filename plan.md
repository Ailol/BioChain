# System Prompt Design: BioChain for Untrained Qwen3.5-A3B-AWQ-4bit

## Goal
Teach the BioChain signal notation system entirely via system prompt so an untrained Qwen3.5-A3B-AWQ-4bit model (running on vLLM Docker) produces output that the existing `BioChainParser.cs` can parse into DB rows.

## Key Constraints
- **3B active params** (MoE model, 35B total) — system prompt must be concise but precise
- **vLLM Docker** — OpenAI-compatible API, no Ollama-specific features
- **Parser-compatible** — every output line must match the regex patterns in BioChainParser.cs
- **No training** — all knowledge comes from the system prompt + in-context examples

## Design: Compact Reference Card + Worked Examples

### Structure (estimated ~2000 tokens)

```
1. ROLE DECLARATION (~50 tokens)
   "You are a neurochemical signal analyzer. Output BioChain notation only."

2. OUTPUT FORMAT RULES (~100 tokens)
   - One TAG: per line
   - TAG must be one of: SIGNAL, RECEPTOR, GATE, LIMITER, TRANSPORT, INTERFACE,
     FEEDBACK, FORMULA, STATE, DEF, DYSREG, HYPOTHESIS, PREDICTION, INTERVENTION
   - Optional suffix: — status: <value>
   - Optional phase grouping: #PHASE: <name>

3. TAG SYNTAX REFERENCE with regex-matching examples (~600 tokens)
   For each of the 7 parser-extractable tags, show exact format + 2 examples:

   SIGNAL: [TYPE:]CODE[state] @REGION
     DA[↑↑] @VTA
     NT:5HT[↓] @DRN

   RECEPTOR: SIGNAL.CODE(subtype)[.state] @REGION
     DA.D2(Gi)[.desens] @NAc

   GATE: {SYMBOL(condition) → effect}
     {⊨(DA > threshold) → PFC.executive[↓]}

   LIMITER: CODE⧫?[activity] → reaction @REGION
     TH⧫[≈] → DA.synthesis @VTA

   TRANSPORT: CODE[state] @REGION
     DAT[≈] @NAc

   INTERFACE: REGION → REGION (pathway)
     VTA → NAc (mesolimbic)

   FORMULA/FEEDBACK/DEF: free-form with signal refs CODE@REGION
     DA@VTA → ... → GLU@PFC

4. VALID VALUES (~200 tokens)
   signal.type: NT | H | P | NI | NS | eCB
   signal.state: ↑↑ | ↑ | ≈ | ↓ | ↓↓
   Common codes: DA, 5HT, NE, GABA, GLU, CORTISOL, OXT, etc.
   Regions: VTA, NAc, PFC, AMY, HPC, DRN, LC, HYP, etc.

5. NON-EXTRACTABLE TAGS (~100 tokens)
   DYSREG: free text (⚡.type markers)
   HYPOTHESIS: free text {confidence: high/medium/low}
   PREDICTION: free text {timeframe: ...}
   INTERVENTION: free text {target: ..., action: ...}

6. WORKED EXAMPLE (~400 tokens)
   Input: short psychological text
   Output: complete BioChain analysis showing all tag types
```

### Why This Design
- **Parser regex compliance**: Each tag example is crafted to match the exact `[GeneratedRegex]` patterns
- **Minimal but sufficient**: ~2000 tokens leaves plenty of room for the user prompt + model output in 4096 context
- **Unicode characters included inline**: ↑↓≈→⧫⊨ etc. shown in examples so the model learns to reproduce them
- **No explanation of theory**: The model doesn't need to understand neuroscience, just produce the notation format

## Deliverables
1. `ollama/system-prompt-biochain-qwen3.5.txt` — the system prompt text
2. `benchmark_qwen3_5_test.py` — test script that:
   - Calls vLLM OpenAI-compatible API with the system prompt
   - Sends 3 test prompts (same as previous benchmarks)
   - Passes output through `BioChainParser.Parse()` equivalent (Python regex port)
   - Scores: parse rate (% lines parsed), tag coverage, signal extraction success

## Implementation Steps
1. Write the system prompt file
2. Write the Python test script with parser regex port
3. User runs vLLM Docker with Qwen3.5-A3B-AWQ-4bit
4. Run test script and evaluate results
