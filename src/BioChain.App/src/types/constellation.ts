// ── Graph data (fast endpoint) ──────────────────────────────

export type NodeKind = 'signal' | 'receptor' | 'transporter' | 'limiter' | 'gate' | 'bind' | 'region' | 'interface';

export interface ConstellationNode {
  id: string;            // unique key: "kind:dbId" or "bind:analysisId"
  code: string;          // human-readable code (DA, 5HT, CORT, mood_regulation, etc.)
  kind: NodeKind;        // signal, receptor, transporter, limiter, gate, bind
  type: string;          // NT, H, P, eCB, NI, R, T, L, B (behavioral composite)
  region: string;
  state: string;         // unicode states: ↑, ↓, ≈, ↑↑, desens, etc.
  community: number;
  confidence: number;
  tauMin?: number | null;
  tauMax?: number | null;
  plasticity?: string | null;
  betweenness: number;
  weight: number;        // mention frequency (e.g. DA×11 → weight=11)
}

export interface ConstellationEdge {
  source: string;
  target: string;
  operator: string;      // e.g. causal+, causal-, feedback+, feedback-, ⊃
  operatorClass: string; // causal, feedback, flow, dysreg, bind
  gain?: number | null;
  delayMs?: number | null;
  dysregType?: string | null;
  active: boolean;
}

export type CommunityStatus = 'functional' | 'compensated' | 'impaired' | 'dysfunctional' | 'collapsed';

export interface ConstellationCommunity {
  id: number;
  name: string;
  code: string;
  status: CommunityStatus;
  description: string;    // short functional description
  signalCount: number;
  elevated: number;
  depleted: number;
  dysregCount: number;
  nodes: string[];
}

export interface FeedbackLoopRow {
  loopPath: string[];
  operators: string[];
  isPositive: boolean;
}

export interface DysregCascadeRow {
  rootCode: string;
  dysregType: string;
  cascadeDepth: number;
  affectedPath: string[];
}

export interface ConstellationBridge {
  node: string;
  between: number[];
  crossEdges: number;
}

export interface ConstellationGeometry {
  shape: string;
  sharpness: number;
  entropy: number;
  polarization: number;
  fragmentation: number;
}

export interface ConstellationGraphResponse {
  nodes: ConstellationNode[];
  edges: ConstellationEdge[];
  communities: ConstellationCommunity[];
  feedbackLoops: FeedbackLoopRow[];
  dysregCascades: DysregCascadeRow[];
  bridges: ConstellationBridge[];
  geometry: ConstellationGeometry;
}

// ── Analysis data (LLM endpoint) ────────────────────────────

export interface AnalysisFix {
  action: string;
  target: string;
  why: string;
  priority: string;
}

export interface AnalysisCommunity {
  id: number;
  name: string;
  status: string;
  summary: string;
  whenWorking: string;
  whenBroken: string;
  fix?: AnalysisFix[] | null;
}

export interface AnalysisNarrative {
  id: string;
  formula: string;
  title: string;
  nodes: string[];
  text: string;
  load: number;
  controlEffort: number;
  fragility: number;
}

export interface AnalysisContradiction {
  id: string;
  surface: string[];
  resolution: string;
  nodes: string[];
  tension: number;
}

export interface AnalysisCompensator {
  id: string;
  what: string;
  masking: string;
  cost: string;
  fragility: string;
  nodes: string[];
  costScore: number;
}

export interface MotifInstance {
  path: string[];
  label: string;
}

export interface AnalysisMotif {
  id: string;
  name: string;
  pattern: string;
  instances: MotifInstance[];
  meaning: string;
}

export interface PerturbationTarget {
  node: string;
  delta: string;
  delay: string;
  mechanism: string;
}

export interface AnalysisPerturbation {
  targets: PerturbationTarget[];
  llm: string;
}

export interface AnalysisArchitecture {
  id: string;
  title: string;
  frame: string;
  text: string;
  nodes: string[];
  severity: string;
}

export interface ConstellationAnalysisResponse {
  communities?: AnalysisCommunity[] | null;
  narratives?: AnalysisNarrative[] | null;
  contradictions?: AnalysisContradiction[] | null;
  compensators?: AnalysisCompensator[] | null;
  motifs?: AnalysisMotif[] | null;
  architecture?: AnalysisArchitecture[] | null;
  perturbations?: Record<string, AnalysisPerturbation> | null;
  humanLabels?: Record<string, string> | null;
}

// ── Shared constants (used across views) ────────────────────

export const HUMAN_LABELS: Record<string, string> = {
  DA: 'Dopamine', '5HT': 'Serotonin', GABA: 'GABA', GLU: 'Glutamate',
  NE: 'Norepinephrine', ACh: 'Acetylcholine', CORT: 'Cortisol',
  OXT: 'Oxytocin', BDNF: 'Brain Growth', MEL: 'Melatonin',
  CRH: 'Stress Signal', ADR: 'Adrenaline', T3: 'Thyroid',
  INS: 'Insulin', AEA: 'Bliss Molecule', '2AG': 'Endocannabinoid',
  VIP: 'Gut Peptide', NPY: 'Neuropeptide Y', SP: 'Substance P',
  DYNORPHIN: 'Dynorphin', ENDORPHIN: 'Endorphin',
  DA_VTA: 'Reward Dopamine', DA_PFC: 'Focus Dopamine',
  '5HT_DRN': 'Mood Serotonin', CORT_HPA: 'Stress Cortisol',
};

export const TYPE_COLORS: Record<string, string> = {
  NT: '#3b82f6', H: '#10b981', P: '#8b5cf6', eCB: '#f59e0b',
  NI: '#06b6d4', R: '#6366f1', T: '#ef4444', L: '#64748b',
  B: '#ec4899',
};

export const EDGE_COLORS: Record<string, string> = {
  causal: '#3b82f680', feedback: '#f59e0b80', flow: '#10b98180', dysreg: '#ef444480',
  bind: '#ec489960',
};

export const COMMUNITY_STATUS_COLORS: Record<string, string> = {
  functional: '#22c55e', compensated: '#f59e0b', impaired: '#f97316',
  dysfunctional: '#ef4444', collapsed: '#7f1d1d',
};

// ── Shared helpers ──────────────────────────────────────────

export function nodeColor(node: ConstellationNode): string {
  return TYPE_COLORS[node.type] ?? '#94a3b8';
}

export function formatBindName(code: string): string {
  return code.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
}

export function nodeSize(node: ConstellationNode): number {
  const base = node.kind === 'bind' ? 14 : node.kind === 'signal' ? 12 : node.kind === 'receptor' ? 8 : node.kind === 'gate' ? 8 : 10;
  const weightBonus = Math.log2(Math.max(node.weight, 1)) * 2.5;
  const betweennessBonus = node.betweenness * 5;
  return base + weightBonus + betweennessBonus;
}

export function stateIcon(state: string): string {
  const map: Record<string, string> = {
    elevated: '\u2B06', depleted: '\u2B07', homeostatic: '\u2248',
    active: '\u25CF', desens: '\u25CC', upreg: '\u25B2', primed: '\u25C6',
  };
  return map[state] ?? state;
}

export function humanLabel(code: string, analysisLabels?: Record<string, string> | null): string {
  return analysisLabels?.[code] ?? HUMAN_LABELS[code]
    ?? (code.includes('_') ? formatBindName(code) : code);
}
