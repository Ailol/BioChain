// Benchmark VLLM parameters for BioChain BNF generation quality
// Tests: temperature, repetition_penalty, frequency_penalty, top_p, top_k
import { readFileSync, writeFileSync } from 'fs';

const VLLM = 'http://localhost:8000/v1/chat/completions';
const MODEL = '/models/Qwen3.5-A3B';
const PROMPT = readFileSync('BioChain.Agent/Prompts/biochain-base.md', 'utf-8');
const USER_INPUT = 'Chronic stress with anhedonia, sleep disruption, and elevated cortisol';
const MAX_TOKENS = 4096;

// Parameter grid to test
const configs = [
  { name: 'baseline',           temperature: 0.3, repetition_penalty: 1.0,  frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.1',             temperature: 0.3, repetition_penalty: 1.1,  frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.15',            temperature: 0.3, repetition_penalty: 1.15, frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.2',             temperature: 0.3, repetition_penalty: 1.2,  frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.3',             temperature: 0.3, repetition_penalty: 1.3,  frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'freq0.3',            temperature: 0.3, repetition_penalty: 1.0,  frequency_penalty: 0.3, top_p: 1.0,  top_k: -1 },
  { name: 'freq0.5',            temperature: 0.3, repetition_penalty: 1.0,  frequency_penalty: 0.5, top_p: 1.0,  top_k: -1 },
  { name: 'freq0.8',            temperature: 0.3, repetition_penalty: 1.0,  frequency_penalty: 0.8, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.15+freq0.3',    temperature: 0.3, repetition_penalty: 1.15, frequency_penalty: 0.3, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.2+freq0.3',     temperature: 0.3, repetition_penalty: 1.2,  frequency_penalty: 0.3, top_p: 1.0,  top_k: -1 },
  { name: 'rep1.15+freq0.5',    temperature: 0.3, repetition_penalty: 1.15, frequency_penalty: 0.5, top_p: 1.0,  top_k: -1 },
  { name: 'temp0.1+rep1.15',    temperature: 0.1, repetition_penalty: 1.15, frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'temp0.5+rep1.15',    temperature: 0.5, repetition_penalty: 1.15, frequency_penalty: 0.0, top_p: 1.0,  top_k: -1 },
  { name: 'temp0.7+rep1.2',     temperature: 0.7, repetition_penalty: 1.2,  frequency_penalty: 0.0, top_p: 0.9,  top_k: 40 },
  { name: 'topp0.9+rep1.15',    temperature: 0.3, repetition_penalty: 1.15, frequency_penalty: 0.0, top_p: 0.9,  top_k: -1 },
  { name: 'topk40+rep1.15',     temperature: 0.3, repetition_penalty: 1.15, frequency_penalty: 0.0, top_p: 1.0,  top_k: 40 },
];

// Quality scoring functions
function scoreOutput(text) {
  const lines = text.split('\n').filter(l => l.trim());
  const scores = {};

  // 1. Has required sections
  scores.has_domain = text.includes('@domain:') ? 1 : 0;
  scores.has_phase = /#\w/.test(text) ? 1 : 0;
  scores.has_delta = /Δ\(/.test(text) ? 1 : 0;
  scores.has_R0 = text.includes('@R0') ? 1 : 0;
  scores.has_R1 = text.includes('@R1') || text.includes('∫{') ? 1 : 0;
  scores.has_R2 = text.includes('@R2') || text.includes('⊲') ? 1 : 0;
  scores.has_R3 = text.includes('@R3') || text.includes('⊗') ? 1 : 0;
  scores.has_conservation = /Σ∇·/.test(text) ? 1 : 0;
  scores.has_composite = text.includes('◈') ? 1 : 0;
  scores.has_dysreg = text.includes('⚡') ? 1 : 0;

  // 2. Section completeness (0-10 scale)
  scores.sections_total = Object.values(scores).reduce((a, b) => a + b, 0);

  // 3. Repetition detection — count repeated identical lines
  const lineCounts = {};
  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.length > 20) {
      lineCounts[trimmed] = (lineCounts[trimmed] || 0) + 1;
    }
  }
  const maxRepeat = Math.max(1, ...Object.values(lineCounts));
  const totalRepeats = Object.values(lineCounts).filter(c => c > 2).reduce((a, b) => a + b, 0);
  scores.max_line_repeat = maxRepeat;
  scores.total_excessive_repeats = totalRepeats;
  scores.repetition_penalty_score = maxRepeat <= 2 ? 10 : maxRepeat <= 4 ? 5 : 0;

  // 4. Node diversity — unique {TYPE:CODE@REGION} patterns
  const nodePattern = /\{([A-Za-z0-9_.]+):([A-Za-z0-9αβγ²⁺⁻]+)(?:\[.*?\])?@([A-Za-z0-9]+)/g;
  const uniqueNodes = new Set();
  let match;
  while ((match = nodePattern.exec(text)) !== null) {
    uniqueNodes.add(`${match[1]}:${match[2]}@${match[3]}`);
  }
  scores.unique_nodes = uniqueNodes.size;
  scores.node_diversity = Math.min(10, uniqueNodes.size / 3);

  // 5. Edge diversity — unique edge operators used
  const edgeOps = ['→', '⊣', '⇌', '⊃', '⊂', '~>', '=>', '|>', '→!', '⊣!'];
  const edgeCount = edgeOps.filter(op => text.includes(op)).length;
  scores.edge_diversity = edgeCount;

  // 6. Region diversity
  const regions = new Set();
  const regionPat = /@([A-Z]{2,5})/g;
  while ((match = regionPat.exec(text)) !== null) {
    if (!['R0', 'R1', 'R2', 'R3'].includes(match[1])) {
      regions.add(match[1]);
    }
  }
  scores.unique_regions = regions.size;

  // 7. No English prose (penalize natural language)
  const proseWords = text.match(/\b(the|is|are|was|were|this|that|which|with|from|into|have|has|been|will|would|could|should)\b/gi) || [];
  scores.prose_words = proseWords.length;
  scores.no_prose_score = proseWords.length === 0 ? 10 : proseWords.length <= 3 ? 7 : proseWords.length <= 10 ? 3 : 0;

  // 8. Output length (meaningful content)
  scores.total_lines = lines.length;
  scores.length_score = lines.length >= 40 ? 10 : lines.length >= 20 ? 7 : lines.length >= 10 ? 4 : 1;

  // COMPOSITE SCORE (weighted)
  scores.composite = (
    scores.sections_total * 3 +       // max 30 (sections coverage)
    scores.repetition_penalty_score * 3 + // max 30 (no repetition)
    scores.node_diversity * 2 +        // max 20 (rich graph)
    scores.no_prose_score * 2 +        // max 20 (pure BNF)
    scores.edge_diversity * 1 +        // max ~10
    scores.length_score * 1            // max 10
  );

  return scores;
}

async function runOne(config) {
  const start = Date.now();

  const body = {
    model: MODEL,
    messages: [
      { role: 'system', content: PROMPT },
      { role: 'user', content: USER_INPUT },
    ],
    temperature: config.temperature,
    max_tokens: MAX_TOKENS,
    repetition_penalty: config.repetition_penalty,
    frequency_penalty: config.frequency_penalty,
    top_p: config.top_p,
  };
  if (config.top_k > 0) body.top_k = config.top_k;

  const res = await fetch(VLLM, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const text = await res.text();
    return { config: config.name, error: text, elapsed: Date.now() - start };
  }

  const data = await res.json();
  const content = data.choices[0]?.message?.content ?? '';
  const elapsed = Date.now() - start;
  const usage = data.usage || {};

  const scores = scoreOutput(content);

  return {
    config: config.name,
    elapsed,
    prompt_tokens: usage.prompt_tokens,
    completion_tokens: usage.completion_tokens,
    ...scores,
    output_preview: content.substring(0, 500),
  };
}

async function main() {
  console.log(`BioChain VLLM Parameter Benchmark`);
  console.log(`Model: ${MODEL}`);
  console.log(`Configs to test: ${configs.length}`);
  console.log(`Input: "${USER_INPUT}"`);
  console.log('─'.repeat(80));

  const results = [];

  for (const config of configs) {
    process.stdout.write(`Testing ${config.name.padEnd(25)}... `);
    const result = await runOne(config);
    if (result.error) {
      console.log(`ERROR: ${result.error.substring(0, 100)}`);
    } else {
      console.log(
        `composite=${String(result.composite).padEnd(5)} ` +
        `sections=${result.sections_total}/10 ` +
        `rep=${result.repetition_penalty_score}/10 ` +
        `nodes=${String(result.unique_nodes).padEnd(3)} ` +
        `prose=${result.prose_words} ` +
        `lines=${String(result.total_lines).padEnd(3)} ` +
        `${(result.elapsed / 1000).toFixed(1)}s`
      );
    }
    results.push(result);
  }

  console.log('\n' + '═'.repeat(80));
  console.log('RANKING (by composite score):');
  console.log('─'.repeat(80));

  const sorted = results
    .filter(r => !r.error)
    .sort((a, b) => b.composite - a.composite);

  for (let i = 0; i < sorted.length; i++) {
    const r = sorted[i];
    console.log(
      `${String(i + 1).padStart(2)}. ${r.config.padEnd(25)} ` +
      `score=${String(r.composite).padEnd(5)} ` +
      `sections=${r.sections_total}/10  rep_score=${r.repetition_penalty_score}/10  ` +
      `nodes=${r.unique_nodes}  regions=${r.unique_regions}  ` +
      `edges=${r.edge_diversity}  prose=${r.prose_words}  ` +
      `max_rep=${r.max_line_repeat}  lines=${r.total_lines}  ` +
      `${(r.elapsed / 1000).toFixed(1)}s`
    );
  }

  // Save full results
  writeFileSync('benchmark_vllm_results.json', JSON.stringify(results, null, 2));
  console.log('\nFull results saved to benchmark_vllm_results.json');

  // Recommend best config
  if (sorted.length > 0) {
    const best = sorted[0];
    const bestConfig = configs.find(c => c.name === best.config);
    console.log(`\nRECOMMENDED: ${best.config}`);
    console.log(`  temperature: ${bestConfig.temperature}`);
    console.log(`  repetition_penalty: ${bestConfig.repetition_penalty}`);
    console.log(`  frequency_penalty: ${bestConfig.frequency_penalty}`);
    console.log(`  top_p: ${bestConfig.top_p}`);
    if (bestConfig.top_k > 0) console.log(`  top_k: ${bestConfig.top_k}`);
  }
}

main().catch(console.error);
