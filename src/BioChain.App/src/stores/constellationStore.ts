import { create } from 'zustand';
import { constellationApi } from '@/api/constellation';
import type {
  ConstellationGraphResponse,
  ConstellationAnalysisResponse,
  NodeKind,
} from '@/types/constellation';

export type ViewMode = 'simple' | 'full';
export type PanelTab = 'systems' | 'person' | 'architecture' | 'whatif';

export const ALL_KINDS: NodeKind[] = ['signal', 'receptor', 'transporter', 'limiter', 'gate', 'bind', 'region', 'interface'];

// ── Focus State (cross-view selection) ─────────────────────

export type FocusTarget =
  | { type: 'entity'; id: string }
  | { type: 'community'; id: number }
  | { type: 'motif'; id: string }
  | { type: 'bind'; id: string }
  | { type: 'gate'; id: string }
  | { type: 'clear' };

export interface FocusState {
  entity: string | null;
  community: number | null;
  motif: string | null;
  bind: string | null;
  gate: string | null;
}

const EMPTY_FOCUS: FocusState = {
  entity: null,
  community: null,
  motif: null,
  bind: null,
  gate: null,
};

// ── Store ──────────────────────────────────────────────────

interface ConstellationState {
  // Data
  graph: ConstellationGraphResponse | null;
  analysis: ConstellationAnalysisResponse | null;

  // Loading states
  graphLoading: boolean;
  analysisLoading: boolean;
  graphError: string | null;
  analysisError: string | null;

  // View mode
  viewMode: ViewMode;
  activeTab: PanelTab;

  // Filtering
  visibleKinds: Set<NodeKind>;
  selectedIntervention: string | null;

  // Focus (cross-view)
  focus: FocusState;
  hoveredNode: string | null;
  cascadeActive: boolean;
  cascadeNodes: Set<string>;

  // Motif expansion
  expandedMotifs: Set<string>;

  // Sidebar
  sidebarOpen: boolean;

  // Actions
  fetchGraph: (subjectId: string, signal?: AbortSignal) => Promise<void>;
  fetchAnalysis: (subjectId: string, signal?: AbortSignal) => Promise<void>;
  setViewMode: (mode: ViewMode) => void;
  setActiveTab: (tab: PanelTab) => void;
  toggleKind: (kind: NodeKind) => void;
  setSelectedIntervention: (key: string | null) => void;
  setFocus: (target: FocusTarget) => void;
  hoverNode: (nodeId: string | null) => void;
  runCascade: (startNode: string) => void;
  stopCascade: () => void;
  toggleMotif: (motifId: string) => void;
  setSidebarOpen: (open: boolean) => void;
  reset: () => void;
}

export const useConstellationStore = create<ConstellationState>((set, get) => ({
  graph: null,
  analysis: null,
  graphLoading: false,
  analysisLoading: false,
  graphError: null,
  analysisError: null,
  viewMode: 'simple',
  activeTab: 'systems',
  visibleKinds: new Set<NodeKind>(ALL_KINDS),
  selectedIntervention: null,
  focus: { ...EMPTY_FOCUS },
  hoveredNode: null,
  cascadeActive: false,
  cascadeNodes: new Set<string>(),
  expandedMotifs: new Set<string>(),
  sidebarOpen: true,

  fetchGraph: async (subjectId, signal) => {
    set({ graphLoading: true, graphError: null });
    try {
      const graph = await constellationApi.getGraph(subjectId, signal);
      if (!signal?.aborted) set({ graph, graphLoading: false });
    } catch (err) {
      if (signal?.aborted) return;
      set({
        graphError: err instanceof Error ? err.message : 'Failed to load graph',
        graphLoading: false,
      });
    }
  },

  fetchAnalysis: async (subjectId, signal) => {
    set({ analysisLoading: true, analysisError: null });
    try {
      const analysis = await constellationApi.analyze(subjectId, signal);
      if (!signal?.aborted) set({ analysis, analysisLoading: false });
    } catch (err) {
      if (signal?.aborted) return;
      set({
        analysisError: err instanceof Error ? err.message : 'Analysis failed',
        analysisLoading: false,
      });
    }
  },

  setViewMode: (mode) => set({ viewMode: mode }),
  setActiveTab: (tab) => set({ activeTab: tab }),

  toggleKind: (kind) =>
    set((s) => {
      const next = new Set(s.visibleKinds);
      if (next.has(kind)) next.delete(kind); else next.add(kind);
      return { visibleKinds: next };
    }),
  setSelectedIntervention: (key) => set({ selectedIntervention: key }),

  setFocus: (target) => {
    switch (target.type) {
      case 'entity':
        set({ focus: { ...EMPTY_FOCUS, entity: target.id } });
        break;
      case 'community':
        set({ focus: { ...EMPTY_FOCUS, community: target.id } });
        break;
      case 'motif':
        set({ focus: { ...EMPTY_FOCUS, motif: target.id } });
        break;
      case 'bind':
        set({ focus: { ...EMPTY_FOCUS, bind: target.id } });
        break;
      case 'gate':
        set({ focus: { ...EMPTY_FOCUS, gate: target.id } });
        break;
      case 'clear':
        set({ focus: { ...EMPTY_FOCUS } });
        break;
    }
  },

  hoverNode: (nodeId) => set({ hoveredNode: nodeId }),

  runCascade: (startNode) => {
    const { graph } = get();
    if (!graph) return;
    const visited = new Set<string>([startNode]);
    const queue = [startNode];
    while (queue.length > 0) {
      const current = queue.shift()!;
      for (const edge of graph.edges) {
        if (edge.source === current && !visited.has(edge.target)) {
          visited.add(edge.target);
          queue.push(edge.target);
        }
      }
    }
    set({ cascadeActive: true, cascadeNodes: visited });
  },

  stopCascade: () => set({ cascadeActive: false, cascadeNodes: new Set() }),

  toggleMotif: (motifId) =>
    set((s) => {
      const next = new Set(s.expandedMotifs);
      if (next.has(motifId)) next.delete(motifId); else next.add(motifId);
      return { expandedMotifs: next };
    }),

  setSidebarOpen: (open) => set({ sidebarOpen: open }),

  reset: () =>
    set({
      graph: null,
      analysis: null,
      graphLoading: false,
      analysisLoading: false,
      graphError: null,
      analysisError: null,
      viewMode: 'simple',
      activeTab: 'systems',
      visibleKinds: new Set<NodeKind>(ALL_KINDS),
      selectedIntervention: null,
      focus: { ...EMPTY_FOCUS },
      hoveredNode: null,
      cascadeActive: false,
      cascadeNodes: new Set(),
      expandedMotifs: new Set(),
      sidebarOpen: true,
    }),
}));
