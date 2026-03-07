import { useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useConstellationStore } from '@/stores/constellationStore';
import { useChatStore } from '@/stores/chatStore';
import {
  Brain, Loader2, AlertTriangle, Layers, Target, Orbit, Zap,
  ChevronRight, ChevronLeft,
} from 'lucide-react';
import type { PanelTab, FocusTarget } from '@/stores/constellationStore';
import { humanLabel } from '@/types/constellation';

// ── View components ─────────────────────────────────────────
import { ViewPanel } from './constellation/ViewPanel';
import { MotifGalaxy } from './constellation/MotifGalaxy';
import { RegionalCircuitMap } from './constellation/RegionalCircuitMap';
import { GateBoard } from './constellation/GateBoard';
import { PhaseCorridor } from './constellation/PhaseCorridor';
import { BindDashboard } from './constellation/BindDashboard';
import { ExplanationCore } from './constellation/ExplanationCore';

// ── Constants ───────────────────────────────────────────────

const TAB_ITEMS: { key: PanelTab; icon: typeof Brain; label: string }[] = [
  { key: 'systems', icon: Layers, label: 'Systems' },
  { key: 'person', icon: Brain, label: 'Person' },
  { key: 'architecture', icon: Target, label: 'Projections' },
  { key: 'whatif', icon: Zap, label: 'What-If' },
];

// ── Main component ──────────────────────────────────────────

export default function ConstellationPage() {
  const { subjectId: paramSubjectId } = useParams<{ subjectId: string }>();
  const navigate = useNavigate();
  const { subjectId: chatSubjectId } = useChatStore();
  const subjectId = paramSubjectId || chatSubjectId;

  const {
    graph, analysis, graphLoading, analysisLoading, graphError, analysisError,
    focus, viewMode, activeTab,
    sidebarOpen, setSidebarOpen,
    fetchGraph, fetchAnalysis, setFocus, setActiveTab, reset,
  } = useConstellationStore();

  const onFocus = useCallback(
    (target: FocusTarget) => setFocus(target),
    [setFocus],
  );

  // Fetch on mount
  useEffect(() => {
    if (!subjectId) return;
    const ac = new AbortController();
    reset();
    fetchGraph(subjectId, ac.signal);
    fetchAnalysis(subjectId, ac.signal);
    return () => ac.abort();
  }, [subjectId, reset, fetchGraph, fetchAnalysis]);

  // ── Guards ──────────────────────────────────────────────
  if (!subjectId) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', background: '#030308' }}>
        <div style={{ textAlign: 'center', maxWidth: 320 }}>
          <Orbit style={{ width: 48, height: 48, color: '#6366f1', margin: '0 auto 16px' }} />
          <h2 style={{ color: '#e2e8f0', fontSize: 18, marginBottom: 8 }}>No Subject Selected</h2>
          <p style={{ color: '#94a3b8', fontSize: 14 }}>Create a profile via the questionnaire first.</p>
          <button onClick={() => navigate('/questionnaire')} style={{ marginTop: 16, padding: '8px 20px', background: '#6366f1', color: '#fff', borderRadius: 12, border: 'none', cursor: 'pointer', fontSize: 14 }}>
            Take Questionnaire
          </button>
        </div>
      </div>
    );
  }

  if (graphLoading) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', background: '#030308', gap: 12 }}>
        <Loader2 style={{ width: 20, height: 20, color: '#94a3b8', animation: 'spin 1s linear infinite' }} />
        <span style={{ color: '#94a3b8', fontSize: 14 }}>Loading constellation graph...</span>
      </div>
    );
  }

  if (graphError) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', background: '#030308' }}>
        <div style={{ textAlign: 'center' }}>
          <AlertTriangle style={{ width: 32, height: 32, color: '#ef4444', margin: '0 auto 8px' }} />
          <p style={{ color: '#ef4444', fontSize: 14 }}>{graphError}</p>
          <button onClick={() => fetchGraph(subjectId)} style={{ color: '#6366f1', background: 'none', border: 'none', cursor: 'pointer', fontSize: 12, marginTop: 8 }}>Retry</button>
        </div>
      </div>
    );
  }

  if (!graph) return null;

  // ── Focus label ─────────────────────────────────────────
  const focusLabel = focus.entity
    ? (() => {
        const node = graph.nodes.find((n) => n.id === focus.entity);
        return node ? humanLabel(node.code, analysis?.humanLabels) : focus.entity;
      })()
    : focus.community !== null
    ? graph.communities.find((c) => c.id === focus.community)?.name ?? `Community ${focus.community}`
    : null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: '#030308', color: '#e2e8f0' }}>
      {/* Top bar */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '6px 12px', background: 'rgba(3,3,8,0.9)', borderBottom: '1px solid #1e293b',
        flexShrink: 0,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Orbit style={{ width: 18, height: 18, color: '#6366f1' }} />
          <span style={{ fontSize: 13, fontWeight: 600 }}>BioChain OS</span>
          <span style={{ fontSize: 10, color: '#64748b' }}>
            {graph.nodes.length} nodes {'\u00B7'} {graph.edges.length} edges {'\u00B7'} {graph.communities.length} systems
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          {focusLabel && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ fontSize: 10, color: '#475569' }}>Focused:</span>
              <span style={{ fontSize: 11, fontWeight: 600, color: '#a5b4fc' }}>{focusLabel}</span>
              <button
                onClick={() => onFocus({ type: 'clear' })}
                style={{ fontSize: 9, color: '#64748b', background: 'none', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
              >
                clear
              </button>
            </div>
          )}
          {analysisLoading && (
            <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: '#38bdf8' }}>
              <Loader2 style={{ width: 10, height: 10, animation: 'spin 1s linear infinite' }} />
              Analyzing...
            </span>
          )}
        </div>
      </div>

      {/* Main content */}
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden', minHeight: 0 }}>
        {/* 2x3 Grid */}
        <div style={{
          flex: 1,
          display: 'grid',
          gridTemplateColumns: 'repeat(3, 1fr)',
          gridTemplateRows: 'repeat(2, 1fr)',
          gap: 4,
          padding: 4,
          minHeight: 0,
          overflow: 'hidden',
        }}>
          {/* Row 1: Structure */}
          <ViewPanel title="Motif Galaxy" icon={<Orbit style={{ width: 10, height: 10, color: '#6366f1' }} />}>
            <MotifGalaxy graph={graph} analysis={analysis} onFocus={onFocus} />
          </ViewPanel>

          <ViewPanel title="Regional Circuit Map" icon={<Brain style={{ width: 10, height: 10, color: '#10b981' }} />}>
            <RegionalCircuitMap graph={graph} analysis={analysis} focus={focus} onFocus={onFocus} />
          </ViewPanel>

          <ViewPanel title="Gate Board" icon={<Target style={{ width: 10, height: 10, color: '#f59e0b' }} />}>
            <GateBoard graph={graph} analysis={analysis} focus={focus} onFocus={onFocus} />
          </ViewPanel>

          {/* Row 2: Dynamics */}
          <ViewPanel title="Phase Corridor" icon={<Layers style={{ width: 10, height: 10, color: '#8b5cf6' }} />}>
            <PhaseCorridor graph={graph} analysis={analysis} focus={focus} onFocus={onFocus} />
          </ViewPanel>

          <ViewPanel title="Bind Dashboard" icon={<Zap style={{ width: 10, height: 10, color: '#ec4899' }} />}>
            <BindDashboard graph={graph} analysis={analysis} focus={focus} onFocus={onFocus} />
          </ViewPanel>

          <ViewPanel title="Explanation Core" icon={<Brain style={{ width: 10, height: 10, color: '#06b6d4' }} />}>
            <ExplanationCore
              graph={graph} analysis={analysis}
              analysisLoading={analysisLoading} analysisError={analysisError}
              focus={focus} activeTab={activeTab} onFocus={onFocus}
            />
          </ViewPanel>
        </div>

        {/* Sidebar toggle */}
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          style={{
            width: 16, background: '#0f172a', border: 'none', borderLeft: '1px solid #1e293b',
            cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#64748b', flexShrink: 0,
          }}
        >
          {sidebarOpen ? <ChevronRight style={{ width: 10, height: 10 }} /> : <ChevronLeft style={{ width: 10, height: 10 }} />}
        </button>

        {/* Sidebar (Explanation Core tab bar + footer) */}
        {sidebarOpen && (
          <div style={{
            width: 320, borderLeft: '1px solid #1e293b', background: '#0a0a12',
            display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0,
          }}>
            {/* Tab bar */}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, padding: 6, borderBottom: '1px solid #1e293b' }}>
              {TAB_ITEMS.map(({ key, icon: Icon, label }) => (
                <button
                  key={key}
                  onClick={() => setActiveTab(key)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 3,
                    padding: '3px 8px', borderRadius: 6, border: 'none', cursor: 'pointer',
                    fontSize: 10, fontWeight: 500,
                    background: activeTab === key ? '#6366f120' : 'transparent',
                    color: activeTab === key ? '#a5b4fc' : '#64748b',
                  }}
                >
                  <Icon style={{ width: 10, height: 10 }} />
                  {label}
                </button>
              ))}
            </div>

            {/* Content (same ExplanationCore, scrollable) */}
            <div style={{ flex: 1, overflowY: 'auto' }}>
              <ExplanationCore
                graph={graph} analysis={analysis}
                analysisLoading={analysisLoading} analysisError={analysisError}
                focus={focus} activeTab={activeTab} onFocus={onFocus}
              />
            </div>

            {/* Footer */}
            <div style={{ borderTop: '1px solid #1e293b', padding: 8 }}>
              <div style={{ display: 'flex', gap: 10, fontSize: 10 }}>
                <span style={{ color: '#22c55e' }}>{graph.feedbackLoops.filter((l) => l.isPositive).length} pos loops</span>
                <span style={{ color: '#ef4444' }}>{graph.feedbackLoops.filter((l) => !l.isPositive).length} neg loops</span>
                <span style={{ color: '#f59e0b' }}>{graph.dysregCascades.length} cascades</span>
                <span style={{ color: '#6366f1' }}>{graph.bridges.length} bridges</span>
              </div>
            </div>
          </div>
        )}
      </div>

      <style>{'@keyframes spin { to { transform: rotate(360deg); } }'}</style>
    </div>
  );
}
