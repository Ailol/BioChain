#!/usr/bin/env node
/**
 * vLLM Parameter Benchmark for BioChain Pipelines
 * Tests: temperature, top_p, top_k, repetition_penalty, min_p
 * Measures: correctness (BNF syntax), depth (entity count), quality (operator diversity), speed (tok/s)
 */

const VLLM_URL = "http://localhost:8000/v1/chat/completions";
const MODEL = "/models/Qwen3.5-a3b-awq-4b";
const CONTEXT_LIMIT = 8192;
const fs = await import("fs");

// ─── Test inputs ────────────────────────────────────────────────────────────
const TEST_INPUT = "Chronic stress with anhedonia, sleep disruption, and neuroinflammation in a 30-year-old male, 18 months duration, no treatment.";

// ─── Load system prompts ────────────────────────────────────────────────────
const PROMPTS_DIR = "system-prompts";
const SYSTEM_PROMPTS = {};
for (const [key, file] of Object.entries({ base: "BASE_SYSTEM_PROMPT.txt", plasticity: "PLASTICITY_SYSTEM_PROMPT.txt", meta: "META_SYSTEM_PROMPT.txt", convergence: "CONVERGENCE_SYSTEM_PROMPT.txt" })) {
  SYSTEM_PROMPTS[key] = fs.readFileSync(`${PROMPTS_DIR}/${file}`, "utf-8");
}

// ─── Parameter grid ─────────────────────────────────────────────────────────
const PARAM_GRID = {
  // Temperature sweep
  "temp_0.1":  { temperature: 0.1, top_p: 0.95 },
  "temp_0.2":  { temperature: 0.2, top_p: 0.95 },
  "temp_0.3":  { temperature: 0.3, top_p: 0.95 },  // current
  "temp_0.5":  { temperature: 0.5, top_p: 0.95 },
  "temp_0.7":  { temperature: 0.7, top_p: 0.95 },

  // top_p variations (with temp=0.3)
  "p_0.8":    { temperature: 0.3, top_p: 0.8 },
  "p_0.9":    { temperature: 0.3, top_p: 0.9 },
  "p_1.0":    { temperature: 0.3, top_p: 1.0 },

  // top_k variations
  "k_20":     { temperature: 0.3, top_p: 0.95, top_k: 20 },
  "k_40":     { temperature: 0.3, top_p: 0.95, top_k: 40 },

  // repetition penalty
  "rep_1.05": { temperature: 0.3, top_p: 0.95, repetition_penalty: 1.05 },
  "rep_1.1":  { temperature: 0.3, top_p: 0.95, repetition_penalty: 1.1 },

  // min_p (cuts low-probability tails)
  "minp_0.05": { temperature: 0.3, top_p: 1.0, min_p: 0.05 },
  "minp_0.1":  { temperature: 0.3, top_p: 1.0, min_p: 0.1 },

  // Combos
  "precise":  { temperature: 0.15, top_p: 0.85, top_k: 40, repetition_penalty: 1.05 },
  "balanced": { temperature: 0.3, top_p: 0.9, min_p: 0.05, repetition_penalty: 1.05 },
};

// ─── Quality metrics ────────────────────────────────────────────────────────

function scoreBnfOutput(text, pipeline) {
  const m = {
    hasProse: /\b(the|this|is|are|it|that|which|however|therefore|because|can|will|should)\b/i.test(text),
    hasMarkdown: /^#{1,3}\s/m.test(text),
    chainCount: (text.match(/⊙/g) || []).length,
    edgeCount: (text.match(/[→⊣~>|>]/g) || []).length,
    integrationCount: (text.match(/∫\{/g) || []).length,
    protocolCount: (text.match(/⊲\{/g) || []).length,
    conditionalCount: (text.match(/⊗\(/g) || []).length,
    ringCount: (text.match(/[«»]|∇×/g) || []).length,
    uniqueOps: new Set(text.match(/[⊙∫⊲⊗∇×«»→⊣⊘◈⚡∮⊳]/g) || []).size,
    regionCodes: new Set((text.match(/@[A-Z]{2,}/g) || []).map(r => r.slice(1))).size,
    nodeDecls: (text.match(/\{[A-Za-z_.]+:[A-Z0-9a-z_]+\[/g) || []).length,
    deltaCount: (text.match(/Δ[0-3]:/g) || []).length,
    metaOps: (text.match(/σ̃|⊗̃|⊲̃|∫̃/g) || []).length,
    convStates: (text.match(/∮\(/g) || []).length,
    trajectories: (text.match(/⊳\(/g) || []).length,
    flags: (text.match(/⚡/g) || []).length,
    lines: text.split("\n").filter(l => l.trim()).length,
    length: text.length,
  };

  // Correctness: no prose, no markdown, has expected BNF structures
  let correctness = 0;
  if (!m.hasProse) correctness += 30;
  if (!m.hasMarkdown) correctness += 10;

  if (pipeline === "base") {
    if (m.chainCount > 0) correctness += 15;
    if (m.integrationCount > 0) correctness += 15;
    if (m.protocolCount > 0) correctness += 10;
    if (m.nodeDecls > 5) correctness += 10;
    if (m.edgeCount > 3) correctness += 10;
  } else if (pipeline === "plasticity") {
    if (m.deltaCount > 0) correctness += 25;
    if (m.deltaCount >= 3) correctness += 15;
    if (m.nodeDecls > 0) correctness += 10;
  } else if (pipeline === "meta") {
    if (m.metaOps > 0) correctness += 25;
    if (m.metaOps >= 3) correctness += 15;
    if (m.nodeDecls > 0) correctness += 10;
  } else if (pipeline === "convergence") {
    if (m.convStates > 0) correctness += 15;
    if (m.trajectories > 0) correctness += 15;
    if (m.flags > 0) correctness += 10;
    if (m.nodeDecls > 0) correctness += 10;
  }

  // Depth: entity richness
  let depth = Math.min(100, (
    m.chainCount * 5 + m.edgeCount * 2 + m.integrationCount * 8 +
    m.protocolCount * 8 + m.conditionalCount * 10 + m.ringCount * 5 +
    m.deltaCount * 10 + m.metaOps * 10 + m.convStates * 8 +
    m.trajectories * 5 + m.flags * 5 + m.regionCodes * 3 + m.nodeDecls * 2
  ));

  // Quality: operator diversity + region coverage
  let quality = Math.min(100, m.uniqueOps * 10 + m.regionCodes * 5);

  return { correctness, depth, quality, m };
}

// ─── Run a single call ──────────────────────────────────────────────────────

const delay = ms => new Promise(r => setTimeout(r, ms));

async function callLlm(systemPrompt, userInput, params) {
  // vLLM's actual tokenizer uses ~2.4 chars/token for this content
  const inputChars = systemPrompt.length + userInput.length;
  const estimatedInputTokens = Math.ceil(inputChars / 2.2) + 250;
  const maxOutputTokens = Math.max(200, Math.min(3500, CONTEXT_LIMIT - estimatedInputTokens - 50));

  const body = {
    model: MODEL,
    messages: [
      { role: "system", content: systemPrompt },
      { role: "user", content: userInput }
    ],
    max_tokens: maxOutputTokens,
    ...params,
    chat_template_kwargs: { enable_thinking: false }
  };

  // Remove default top_k (vLLM doesn't accept -1)
  if (body.top_k === undefined || body.top_k === -1) delete body.top_k;

  const start = performance.now();
  const resp = await fetch(VLLM_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const elapsed = performance.now() - start;

  if (!resp.ok) {
    const errText = await resp.text();
    throw new Error(`LLM ${resp.status}: ${errText.slice(0, 300)}`);
  }

  const data = await resp.json();
  const choice = data.choices[0];
  const content = (choice.message?.content || "").replace(/<think>[\s\S]*?<\/think>/g, "").trim();
  const usage = data.usage || {};

  return {
    content,
    promptTokens: usage.prompt_tokens || 0,
    completionTokens: usage.completion_tokens || 0,
    elapsedMs: Math.round(elapsed),
    tokPerSec: usage.completion_tokens ? Math.round(usage.completion_tokens / (elapsed / 1000) * 10) / 10 : 0,
    finishReason: choice.finish_reason,
    maxOutputTokens,
  };
}

// ─── Truncate BNF output to fit context ──────────────────────────────────────
function truncateBnf(text, maxChars) {
  if (text.length <= maxChars) return text;
  const truncated = text.slice(0, maxChars);
  const lastNewline = truncated.lastIndexOf("\n");
  return (lastNewline > 0 ? truncated.slice(0, lastNewline) : truncated) + "\n// ... truncated";
}

// ─── Main benchmark ─────────────────────────────────────────────────────────

async function runBenchmark() {
  console.log("=== vLLM Parameter Benchmark for BioChain ===\n");

  // Health check with retry
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const resp = await fetch("http://localhost:8000/v1/models");
      if (resp.ok) { console.log("vLLM ready.\n"); break; }
    } catch {}
    if (attempt === 4) { console.error("vLLM not available."); process.exit(1); }
    console.log(`Waiting for vLLM... (attempt ${attempt + 1})`);
    await delay(10000);
  }

  const results = [];
  const paramNames = Object.keys(PARAM_GRID);

  // ─── Phase 1: BASE pipeline (all configs) ────────────────────────────────
  console.log("━━━ Phase 1: BASE Pipeline ━━━");
  console.log(`Testing ${paramNames.length} configs...\n`);

  const baseOutputs = {};

  for (const [name, params] of Object.entries(PARAM_GRID)) {
    process.stdout.write(`  [${name.padEnd(10)}] `);
    try {
      const r = await callLlm(SYSTEM_PROMPTS.base, TEST_INPUT, params);
      const s = scoreBnfOutput(r.content, "base");

      results.push({
        pipeline: "base", params: name, ...params,
        correctness: s.correctness, depth: s.depth, quality: s.quality,
        tokPerSec: r.tokPerSec, completionTokens: r.completionTokens,
        elapsedMs: r.elapsedMs, finishReason: r.finishReason,
        hasProse: s.m.hasProse, chains: s.m.chainCount,
        integrations: s.m.integrationCount, protocols: s.m.protocolCount,
        regions: s.m.regionCodes, nodes: s.m.nodeDecls, lines: s.m.lines,
        maxOut: r.maxOutputTokens, promptTok: r.promptTokens,
      });
      baseOutputs[name] = r.content;

      const fin = r.finishReason === "length" ? " TRUNC" : "";
      console.log(`C:${String(s.correctness).padStart(3)} D:${String(s.depth).padStart(3)} Q:${String(s.quality).padStart(3)} | ${String(r.tokPerSec).padStart(5)}t/s ${String(r.completionTokens).padStart(4)}tok ${String(r.elapsedMs).padStart(6)}ms | ⊙:${s.m.chainCount} ∫:${s.m.integrationCount} ⊲:${s.m.protocolCount} @:${s.m.regionCodes} prose:${s.m.hasProse}${fin}`);
    } catch (e) {
      console.log(`ERROR: ${e.message.slice(0, 120)}`);
      results.push({ pipeline: "base", params: name, error: e.message });
    }
    await delay(1500); // prevent engine overload
  }

  // ─── Phase 2: Pick top-3 BASE configs, test downstream ───────────────────
  const baseOk = results.filter(r => r.pipeline === "base" && !r.error);
  baseOk.sort((a, b) => (b.correctness * 2 + b.depth + b.quality) - (a.correctness * 2 + a.depth + a.quality));

  const topConfigs = [...new Set([...baseOk.slice(0, 3).map(r => r.params), "temp_0.3"])];
  console.log(`\nTop configs for downstream: ${topConfigs.join(", ")}\n`);

  for (const cfgName of topConfigs) {
    const params = PARAM_GRID[cfgName];
    const baseOut = baseOutputs[cfgName];
    if (!baseOut) continue;

    const maxPriorChars = 3000;

    // ─── PLASTICITY ─────────────────────────────────────────────────────
    console.log(`━━━ PLASTICITY [${cfgName}] ━━━`);
    try {
      const plasticityInput = `BASE:\n${truncateBnf(baseOut, maxPriorChars)}\n\nInput: ${TEST_INPUT}\nMode: PREDICTIVE. Project all Δ cascades.`;
      const r = await callLlm(SYSTEM_PROMPTS.plasticity, plasticityInput, params);
      const s = scoreBnfOutput(r.content, "plasticity");

      results.push({
        pipeline: "plasticity", params: cfgName,
        correctness: s.correctness, depth: s.depth, quality: s.quality,
        tokPerSec: r.tokPerSec, completionTokens: r.completionTokens,
        elapsedMs: r.elapsedMs, finishReason: r.finishReason,
        hasProse: s.m.hasProse, deltas: s.m.deltaCount, lines: s.m.lines,
        promptTok: r.promptTokens,
      });

      const fin = r.finishReason === "length" ? " TRUNC" : "";
      console.log(`  C:${s.correctness} D:${s.depth} Q:${s.quality} | ${r.tokPerSec}t/s ${r.completionTokens}tok | Δ:${s.m.deltaCount} prose:${s.m.hasProse}${fin}`);

      await delay(1500);

      // ─── META ─────────────────────────────────────────────────────────
      console.log(`━━━ META [${cfgName}] ━━━`);
      const metaInput = `BASE:\n${truncateBnf(baseOut, 1500)}\nPLASTICITY:\n${truncateBnf(r.content, 1500)}\n\nInput: ${TEST_INPUT}`;
      const mr = await callLlm(SYSTEM_PROMPTS.meta, metaInput, params);
      const ms = scoreBnfOutput(mr.content, "meta");

      results.push({
        pipeline: "meta", params: cfgName,
        correctness: ms.correctness, depth: ms.depth, quality: ms.quality,
        tokPerSec: mr.tokPerSec, completionTokens: mr.completionTokens,
        elapsedMs: mr.elapsedMs, finishReason: mr.finishReason,
        hasProse: ms.m.hasProse, metaOps: ms.m.metaOps, lines: ms.m.lines,
        promptTok: mr.promptTokens,
      });

      const mfin = mr.finishReason === "length" ? " TRUNC" : "";
      console.log(`  C:${ms.correctness} D:${ms.depth} Q:${ms.quality} | ${mr.tokPerSec}t/s ${mr.completionTokens}tok | meta:${ms.m.metaOps} prose:${ms.m.hasProse}${mfin}`);

      await delay(1500);

      // ─── CONVERGENCE ──────────────────────────────────────────────────
      console.log(`━━━ CONVERGENCE [${cfgName}] ━━━`);
      const convInput = `BASE:\n${truncateBnf(baseOut, 1000)}\nΔ:\n${truncateBnf(r.content, 1000)}\nMETA:\n${truncateBnf(mr.content, 1000)}\n\nInput: ${TEST_INPUT}`;
      const cr = await callLlm(SYSTEM_PROMPTS.convergence, convInput, params);
      const cs = scoreBnfOutput(cr.content, "convergence");

      results.push({
        pipeline: "convergence", params: cfgName,
        correctness: cs.correctness, depth: cs.depth, quality: cs.quality,
        tokPerSec: cr.tokPerSec, completionTokens: cr.completionTokens,
        elapsedMs: cr.elapsedMs, finishReason: cr.finishReason,
        hasProse: cs.m.hasProse, convStates: cs.m.convStates,
        trajectories: cs.m.trajectories, flags: cs.m.flags, lines: cs.m.lines,
        promptTok: cr.promptTokens,
      });

      const cfin = cr.finishReason === "length" ? " TRUNC" : "";
      console.log(`  C:${cs.correctness} D:${cs.depth} Q:${cs.quality} | ${cr.tokPerSec}t/s ${cr.completionTokens}tok | ∮:${cs.m.convStates} ⊳:${cs.m.trajectories} ⚡:${cs.m.flags}${cfin}`);

    } catch (e) {
      console.log(`  ERROR: ${e.message.slice(0, 200)}`);
    }
    await delay(2000);
    console.log();
  }

  // ─── Save results ─────────────────────────────────────────────────────────
  fs.writeFileSync("benchmark_vllm_results.json", JSON.stringify(results, null, 2));
  console.log("Results saved to benchmark_vllm_results.json\n");

  // ─── Summary table ────────────────────────────────────────────────────────
  console.log("═══ SUMMARY ═══\n");

  for (const pipeline of ["base", "plasticity", "meta", "convergence"]) {
    const pr = results.filter(r => r.pipeline === pipeline && !r.error);
    if (pr.length === 0) continue;

    console.log(`─── ${pipeline.toUpperCase()} ───`);
    console.log(
      "Config".padEnd(14) +
      "Corr".padStart(5) + "Dpth".padStart(5) + "Qual".padStart(5) +
      " | " + "t/s".padStart(6) + "tok".padStart(5) + "ms".padStart(7) +
      " | " + "prose".padStart(5) + " fin".padStart(8)
    );

    for (const r of pr) {
      console.log(
        (r.params || "?").padEnd(14) +
        String(r.correctness).padStart(5) +
        String(r.depth).padStart(5) +
        String(r.quality).padStart(5) +
        " | " + String(r.tokPerSec).padStart(6) +
        String(r.completionTokens).padStart(5) +
        `${r.elapsedMs}`.padStart(7) +
        " | " + String(r.hasProse).padStart(5) +
        ` ${(r.finishReason || "?").padStart(7)}`
      );
    }
    console.log();
  }

  // ─── Recommendation ───────────────────────────────────────────────────────
  console.log("═══ RECOMMENDATION ═══\n");

  const configAgg = {};
  for (const r of results.filter(r => !r.error)) {
    if (!configAgg[r.params]) configAgg[r.params] = { total: 0, count: 0, speed: 0 };
    configAgg[r.params].total += r.correctness * 2 + r.depth + r.quality;
    configAgg[r.params].count++;
    configAgg[r.params].speed += r.tokPerSec;
  }

  const ranked = Object.entries(configAgg)
    .map(([n, s]) => ({ n, score: s.total / s.count, speed: s.speed / s.count }))
    .sort((a, b) => b.score - a.score);

  console.log("Rank | Config         | Score | Speed");
  for (let i = 0; i < ranked.length; i++) {
    const r = ranked[i];
    console.log(`  ${String(i+1).padStart(2)} | ${r.n.padEnd(14)} | ${Math.round(r.score).toString().padStart(5)} | ${r.speed.toFixed(1)} t/s`);
  }

  // Speed optimized
  const speedRanked = [...ranked].sort((a, b) => b.speed - a.speed);
  console.log(`\nFastest: ${speedRanked[0]?.n} (${speedRanked[0]?.speed.toFixed(1)} t/s, score: ${Math.round(speedRanked[0]?.score)})`);
  console.log(`Best quality: ${ranked[0]?.n} (score: ${Math.round(ranked[0]?.score)}, ${ranked[0]?.speed.toFixed(1)} t/s)`);
}

runBenchmark().catch(console.error);
