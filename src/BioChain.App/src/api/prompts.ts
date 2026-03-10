// System prompt loader — fetches from public/prompts/

const cache: Record<string, string> = {};

export type PipelineStage = 'base' | 'plasticity' | 'meta' | 'convergence';

const promptFiles: Record<PipelineStage, string> = {
  base: '/prompts/biochain-base.md',
  plasticity: '/prompts/biochain-plasticity.md',
  meta: '/prompts/biochain-meta.md',
  convergence: '/prompts/biochain-convergence.md',
};

export async function loadPrompt(stage: PipelineStage): Promise<string> {
  if (cache[stage]) return cache[stage];

  const res = await fetch(promptFiles[stage]);
  if (!res.ok) throw new Error(`Failed to load prompt for ${stage}: ${res.status}`);

  const text = await res.text();
  cache[stage] = text;
  return text;
}
