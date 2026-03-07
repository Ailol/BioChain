import { useEffect, useRef, useState, useCallback } from 'react';
import { useConstellationStore } from '@/stores/constellationStore';
import type { FocusTarget } from '@/stores/constellationStore';
import Graph from 'graphology';
import Sigma from 'sigma';
import forceAtlas2 from 'graphology-layout-forceatlas2';
import { EdgeCurvedArrowProgram } from '@sigma/edge-curve';
import { Eye, EyeOff } from 'lucide-react';
import type { ConstellationNode, ConstellationGraphResponse, ConstellationAnalysisResponse, NodeKind } from '@/types/constellation';
import { HUMAN_LABELS, TYPE_COLORS, EDGE_COLORS, COMMUNITY_STATUS_COLORS, nodeColor, nodeSize, stateIcon, formatBindName, humanLabel } from '@/types/constellation';
import type { ViewMode } from '@/stores/constellationStore';

// ── Constants ───────────────────────────────────────────────

const KIND_FILTERS: { kind: NodeKind; label: string }[] = [
  { kind: 'signal', label: 'Signal' },
  { kind: 'receptor', label: 'Receptor' },
  { kind: 'transporter', label: 'Transport' },
  { kind: 'limiter', label: 'Limiter' },
  { kind: 'gate', label: 'Gate' },
  { kind: 'bind', label: 'Bind' },
  { kind: 'region', label: 'Region' },
  { kind: 'interface', label: 'Interface' },
];

function pillBtnStyle(bg: string): React.CSSProperties {
  return {
    padding: '4px 12px', fontSize: 10, fontWeight: 600,
    background: bg + '30', color: bg === '#334155' ? '#94a3b8' : bg,
    border: '1px solid ' + bg + '40', borderRadius: 8, cursor: 'pointer',
  };
}

function GeoBadge({ label, value }: { label: string; value: string }) {
  return (
    <div style={{
      padding: '3px 8px', background: 'rgba(15,23,42,0.8)', borderRadius: 6,
      border: '1px solid #1e293b', backdropFilter: 'blur(4px)',
    }}>
      <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase' }}>{label}</div>
      <div style={{ fontSize: 11, color: '#e2e8f0', fontFamily: 'monospace' }}>{value}</div>
    </div>
  );
}

function Lbl({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) {
  return <span style={style}>{children}</span>;
}

// ── Nebula drawing ────────────────────────────────────────────

function drawNebulae(
  sigma: Sigma,
  g: Graph,
  communities: { id: number; name: string; status: string; nodes: string[] }[],
  canvas: HTMLCanvasElement | null,
) {
  if (!canvas) return;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  const dpr = window.devicePixelRatio || 1;
  canvas.width = canvas.offsetWidth * dpr;
  canvas.height = canvas.offsetHeight * dpr;
  ctx.scale(dpr, dpr);
  ctx.clearRect(0, 0, canvas.offsetWidth, canvas.offsetHeight);

  for (const com of communities) {
    const positions = com.nodes
      .filter((id) => g.hasNode(id))
      .map((id) => {
        const attrs = g.getNodeAttributes(id);
        return sigma.graphToViewport({ x: attrs.x as number, y: attrs.y as number });
      });

    if (positions.length < 2) continue;

    const cx = positions.reduce((s, p) => s + p.x, 0) / positions.length;
    const cy = positions.reduce((s, p) => s + p.y, 0) / positions.length;
    const maxDist = positions.reduce((m, p) => {
      const d = Math.sqrt((p.x - cx) ** 2 + (p.y - cy) ** 2);
      return Math.max(m, d);
    }, 0);
    const radius = maxDist + 40;

    const statusColor = COMMUNITY_STATUS_COLORS[com.status] ?? '#64748b';

    const gradient = ctx.createRadialGradient(cx, cy, 0, cx, cy, radius);
    gradient.addColorStop(0, statusColor + '15');
    gradient.addColorStop(0.7, statusColor + '08');
    gradient.addColorStop(1, statusColor + '00');

    ctx.fillStyle = gradient;
    ctx.beginPath();
    ctx.arc(cx, cy, radius, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = statusColor + '30';
    ctx.lineWidth = 1;
    ctx.setLineDash([4, 4]);
    ctx.beginPath();
    ctx.arc(cx, cy, radius, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);

    ctx.fillStyle = statusColor + 'cc';
    ctx.font = '10px Inter, system-ui, sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(com.name, cx, cy - radius - 6);

    ctx.fillStyle = statusColor + '40';
    const badgeText = com.status.toUpperCase();
    const badgeWidth = ctx.measureText(badgeText).width + 8;
    ctx.fillRect(cx - badgeWidth / 2, cy - radius - 20, badgeWidth, 12);
    ctx.fillStyle = statusColor;
    ctx.font = 'bold 8px Inter, system-ui, sans-serif';
    ctx.fillText(badgeText, cx, cy - radius - 11);
  }
}

// ── Dashed/dotted edge overlay + compensation bridges ────────

function drawStyledEdges(
  sigma: Sigma,
  g: Graph,
  canvas: HTMLCanvasElement | null,
  compensators?: { nodes: string[] }[],
) {
  if (!canvas) return;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  const dpr = window.devicePixelRatio || 1;
  canvas.width = canvas.offsetWidth * dpr;
  canvas.height = canvas.offsetHeight * dpr;
  ctx.scale(dpr, dpr);
  ctx.clearRect(0, 0, canvas.offsetWidth, canvas.offsetHeight);

  const st = useConstellationStore.getState();

  // Draw dashed/dotted edges
  g.forEachEdge((edgeId, attrs, src, tgt) => {
    const cls = attrs._operatorClass as string;
    if (cls !== 'dysreg' && cls !== 'bind') return;

    const srcKind = g.getNodeAttribute(src, '_kind') as string;
    const tgtKind = g.getNodeAttribute(tgt, '_kind') as string;
    if (!st.visibleKinds.has(srcKind as NodeKind) || !st.visibleKinds.has(tgtKind as NodeKind)) return;

    const srcPos = sigma.graphToViewport(g.getNodeAttributes(src) as { x: number; y: number });
    const tgtPos = sigma.graphToViewport(g.getNodeAttributes(tgt) as { x: number; y: number });

    ctx.strokeStyle = cls === 'dysreg' ? '#ef444480' : '#ec489960';
    ctx.lineWidth = cls === 'dysreg' ? 2 : 1.5;
    ctx.setLineDash(cls === 'dysreg' ? [6, 4] : [2, 3]);

    ctx.beginPath();
    ctx.moveTo(srcPos.x, srcPos.y);
    ctx.lineTo(tgtPos.x, tgtPos.y);
    ctx.stroke();
  });

  ctx.setLineDash([]);

  // Draw compensation bridges
  if (compensators && compensators.length > 0) {
    for (const comp of compensators) {
      if (comp.nodes.length < 2) continue;
      const positions = comp.nodes
        .map((code): string | null => {
          let nodeId: string | null = null;
          g.forEachNode((id, attrs) => {
            if ((attrs._code as string) === code) nodeId = id;
          });
          return nodeId;
        })
        .filter((id): id is string => id !== null && g.hasNode(id!))
        .map((id) => sigma.graphToViewport(g.getNodeAttributes(id) as { x: number; y: number }));

      if (positions.length < 2) continue;

      // Draw double-line bridge
      ctx.strokeStyle = '#f59e0b60';
      ctx.lineWidth = 3;
      ctx.setLineDash([8, 4]);
      ctx.beginPath();
      ctx.moveTo(positions[0].x, positions[0].y);
      for (let i = 1; i < positions.length; i++) {
        ctx.lineTo(positions[i].x, positions[i].y);
      }
      ctx.stroke();

      // Second line offset
      ctx.strokeStyle = '#f59e0b30';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(positions[0].x + 2, positions[0].y + 2);
      for (let i = 1; i < positions.length; i++) {
        ctx.lineTo(positions[i].x + 2, positions[i].y + 2);
      }
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }
}

// ── MotifGalaxy Component ───────────────────────────────────

interface MotifGalaxyProps {
  graph: ConstellationGraphResponse;
  analysis: ConstellationAnalysisResponse | null;
  onFocus: (target: FocusTarget) => void;
}

export function MotifGalaxy({ graph, analysis, onFocus }: MotifGalaxyProps) {
  const {
    viewMode, visibleKinds, selectedIntervention,
    focus, hoveredNode, cascadeActive, cascadeNodes,
    hoverNode, runCascade, stopCascade, toggleKind,
    setViewMode,
  } = useConstellationStore();

  const containerRef = useRef<HTMLDivElement>(null);
  const nebulaRef = useRef<HTMLCanvasElement>(null);
  const edgeOverlayRef = useRef<HTMLCanvasElement>(null);
  const sigmaRef = useRef<Sigma | null>(null);
  const graphologyRef = useRef<Graph | null>(null);
  const [tooltip, setTooltip] = useState<{ node: ConstellationNode; x: number; y: number } | null>(null);

  const hl = useCallback(
    (code: string) => humanLabel(code, analysis?.humanLabels),
    [analysis],
  );

  // Build graph + Sigma
  useEffect(() => {
    if (!graph || !containerRef.current) return;

    const g = new Graph();

    for (const node of graph.nodes) {
      g.addNode(node.id, {
        x: Math.random() * 100, y: Math.random() * 100,
        size: nodeSize(node), color: nodeColor(node),
        label: viewMode === 'simple' ? hl(node.code) : node.code,
        _kind: node.kind, _type: node.type, _state: node.state,
        _community: node.community, _weight: node.weight,
        _betweenness: node.betweenness, _confidence: node.confidence,
        _tauMin: node.tauMin, _tauMax: node.tauMax,
        _plasticity: node.plasticity, _region: node.region, _code: node.code,
      });
    }

    for (const edge of graph.edges) {
      if (!g.hasNode(edge.source) || !g.hasNode(edge.target)) continue;
      try {
        const isFeedback = edge.operatorClass === 'feedback';
        g.addEdge(edge.source, edge.target, {
          color: EDGE_COLORS[edge.operatorClass] ?? '#64748b40',
          size: edge.operatorClass === 'dysreg' ? 2.5 : isFeedback ? 1.5 : 1,
          type: isFeedback ? 'curved' : 'line',
          _operator: edge.operator, _operatorClass: edge.operatorClass,
          _dysregType: edge.dysregType, _active: edge.active,
        });
      } catch { /* duplicate edge */ }
    }

    const settings = forceAtlas2.inferSettings(g);
    forceAtlas2.assign(g, {
      iterations: 200,
      settings: { ...settings, gravity: 1, scalingRatio: 10, barnesHutOptimize: g.order > 100 },
    });

    graphologyRef.current = g;

    const renderer = new Sigma(g, containerRef.current, {
      defaultNodeColor: '#94a3b8',
      defaultEdgeColor: '#64748b40',
      labelFont: 'Inter, system-ui, sans-serif',
      labelSize: 12, labelWeight: '500',
      labelColor: { color: '#e2e8f0' },
      labelDensity: 0.8, labelRenderedSizeThreshold: 2,
      renderLabels: true, renderEdgeLabels: false,
      enableEdgeEvents: false, stagePadding: 40,
      zIndex: true, minEdgeThickness: 0.5,
      edgeProgramClasses: { curved: EdgeCurvedArrowProgram },
      nodeReducer: (nodeId, data) => {
        const st = useConstellationStore.getState();
        const res = { ...data };
        const kind = (data as any)._kind as string;
        const code = (data as any)._code as string | undefined;
        const state = (data as any)._state as string | undefined;
        const w = (data as any)._weight as number | undefined;

        // Kind filtering
        if (!st.visibleKinds.has(kind as NodeKind)) {
          res.hidden = true;
          return res;
        }

        // viewMode-aware sizing
        if (st.viewMode === 'simple') {
          if (kind === 'bind') {
            res.size = (res.size || 12) * 1.4;
            res.zIndex = 5;
          } else {
            res.size = (res.size || 12) * 0.75;
          }
        } else if (kind === 'bind') {
          res.size = (res.size || 12) * 0.8;
        }

        // Cascade dimming
        if (st.cascadeActive && !st.cascadeNodes.has(nodeId)) {
          res.color = '#1e293b'; res.label = null;
          return res;
        }

        // Hover dimming
        if (st.hoveredNode && st.hoveredNode !== nodeId) {
          const gr = graphologyRef.current;
          if (gr && !gr.hasEdge(st.hoveredNode, nodeId) && !gr.hasEdge(nodeId, st.hoveredNode)) {
            res.color = (res.color || '#94a3b8') + '40';
          }
        }

        // Focus highlight
        if (st.focus.entity && st.focus.entity !== nodeId) {
          const gr = graphologyRef.current;
          if (gr && !gr.hasEdge(st.focus.entity, nodeId) && !gr.hasEdge(nodeId, st.focus.entity)) {
            res.color = (res.color || '#94a3b8').slice(0, 7) + '50';
          }
        }
        if (st.focus.entity === nodeId) {
          res.highlighted = true;
          res.zIndex = 10;
        }

        // Community focus
        if (st.focus.community !== null) {
          const nodeCom = (data as any)._community as number;
          if (nodeCom !== st.focus.community) {
            res.color = (res.color || '#94a3b8').slice(0, 7) + '30';
          }
        }

        // Intervention highlight
        if (st.selectedIntervention && st.analysis?.perturbations) {
          const p = st.analysis.perturbations[st.selectedIntervention];
          if (p) {
            const isTarget = p.targets.some((t) => t.node === code);
            if (!isTarget) {
              res.color = (res.color || '#94a3b8').slice(0, 7) + '30';
            } else {
              res.zIndex = 10;
              res.highlighted = true;
            }
          }
        }

        // Simple mode: fade non-bind when idle
        if (st.viewMode === 'simple' && kind !== 'bind' && !st.hoveredNode && !st.cascadeActive && !st.selectedIntervention && !st.focus.entity) {
          res.color = (res.color || '#94a3b8').slice(0, 7) + '90';
        }

        // Labels with state
        const showState = kind !== 'region' && kind !== 'interface' && state && state !== 'unknown';
        if (kind === 'bind' && code) {
          res.label = st.viewMode === 'simple'
            ? formatBindName(code) + (showState ? '\n' + stateIcon(state!) + ' ' + state : '')
            : code;
        } else if (kind === 'region' && code) {
          res.label = code;
        } else if (kind === 'interface' && code) {
          res.label = code + ' \u25CF active';
        } else if (code) {
          const label = st.viewMode === 'simple'
            ? (HUMAN_LABELS[code] ?? code) + (w && w > 3 ? ' (' + w + '\u00D7)' : '')
            : code + (w && w > 3 ? ' \u00D7' + w : '');
          res.label = st.viewMode === 'simple' && showState
            ? label + '\n' + stateIcon(state!) + ' ' + state
            : label;
        }
        return res;
      },
      edgeReducer: (edgeId, data) => {
        const st = useConstellationStore.getState();
        const res = { ...data };
        const gr = graphologyRef.current;
        if (!gr) return res;

        const src = gr.source(edgeId);
        const tgt = gr.target(edgeId);
        const srcKind = gr.getNodeAttribute(src, '_kind') as string;
        const tgtKind = gr.getNodeAttribute(tgt, '_kind') as string;

        if (!st.visibleKinds.has(srcKind as NodeKind) || !st.visibleKinds.has(tgtKind as NodeKind)) {
          res.hidden = true;
          return res;
        }

        if (st.cascadeActive) {
          if (!st.cascadeNodes.has(src) || !st.cascadeNodes.has(tgt)) res.hidden = true;
        }

        if (st.hoveredNode) {
          if (src !== st.hoveredNode && tgt !== st.hoveredNode) res.color = '#1e293b20';
        }

        // Focus entity edge highlight
        if (st.focus.entity) {
          if (src !== st.focus.entity && tgt !== st.focus.entity) res.color = '#1e293b20';
        }

        if (st.selectedIntervention && st.analysis?.perturbations) {
          const p = st.analysis.perturbations[st.selectedIntervention];
          if (p) {
            const srcCode = gr.getNodeAttribute(src, '_code') as string;
            const tgtCode = gr.getNodeAttribute(tgt, '_code') as string;
            const srcTarget = p.targets.some((t) => t.node === srcCode);
            const tgtTarget = p.targets.some((t) => t.node === tgtCode);
            if (!srcTarget && !tgtTarget) res.color = '#1e293b15';
          }
        }

        return res;
      },
    });

    sigmaRef.current = renderer;

    renderer.on('enterNode', ({ node }) => {
      hoverNode(node);
      const nd = graph.nodes.find((n) => n.id === node);
      if (nd) {
        const vp = renderer.graphToViewport(g.getNodeAttributes(node) as { x: number; y: number });
        setTooltip({ node: nd, x: vp.x, y: vp.y });
      }
      renderer.refresh();
    });
    renderer.on('leaveNode', () => { hoverNode(null); setTooltip(null); renderer.refresh(); });
    renderer.on('clickNode', ({ node }) => {
      const st = useConstellationStore.getState();
      if (st.focus.entity === node) {
        onFocus({ type: 'clear' });
      } else {
        onFocus({ type: 'entity', id: node });
      }
      renderer.refresh();
    });
    renderer.on('doubleClickNode', ({ node }) => { runCascade(node); renderer.refresh(); });
    renderer.on('clickStage', () => {
      onFocus({ type: 'clear' });
      if (useConstellationStore.getState().cascadeActive) stopCascade();
      renderer.refresh();
    });
    renderer.on('afterRender', () => {
      drawNebulae(renderer, g, graph.communities, nebulaRef.current);
      drawStyledEdges(renderer, g, edgeOverlayRef.current, analysis?.compensators ?? undefined);
    });

    return () => { renderer.kill(); sigmaRef.current = null; graphologyRef.current = null; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [graph]);

  // Refresh on state change
  useEffect(() => {
    const renderer = sigmaRef.current;
    const g = graphologyRef.current;
    if (!renderer || !g) return;
    g.forEachNode((nodeId, attrs) => {
      const code = attrs._code as string;
      const kind = attrs._kind as string;
      const weight = attrs._weight as number;
      if (kind === 'bind') {
        g.setNodeAttribute(nodeId, 'label', viewMode === 'simple' ? formatBindName(code) : code);
      } else {
        const label = viewMode === 'simple' ? (HUMAN_LABELS[code] ?? analysis?.humanLabels?.[code] ?? code) : code;
        g.setNodeAttribute(nodeId, 'label',
          weight > 3
            ? viewMode === 'simple' ? label + ' (' + weight + '\u00D7)' : code + ' \u00D7' + weight
            : label,
        );
      }
    });
    renderer.refresh();
  }, [viewMode, cascadeActive, cascadeNodes, focus, hoveredNode, analysis,
      visibleKinds, selectedIntervention]);

  const focusedNodeData = focus.entity ? graph.nodes.find((n) => n.id === focus.entity) : null;

  return (
    <div style={{ width: '100%', height: '100%', position: 'relative', overflow: 'hidden' }}>
      {/* Controls overlay */}
      <div style={{ position: 'absolute', top: 8, left: 8, zIndex: 10, display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', background: '#0f172a', borderRadius: 8, overflow: 'hidden', border: '1px solid #1e293b' }}>
          {(['simple', 'full'] as ViewMode[]).map((mode) => (
            <button key={mode} onClick={() => setViewMode(mode)} style={{ padding: '3px 10px', fontSize: 10, fontWeight: 600, border: 'none', cursor: 'pointer', background: viewMode === mode ? '#6366f1' : 'transparent', color: viewMode === mode ? '#fff' : '#64748b', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              {mode}
            </button>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
          {KIND_FILTERS.map(({ kind, label }) => {
            const active = visibleKinds.has(kind);
            return (
              <button key={kind} onClick={() => toggleKind(kind)} style={{ display: 'flex', alignItems: 'center', gap: 3, padding: '2px 6px', fontSize: 8, fontWeight: 600, letterSpacing: '0.05em', border: '1px solid', borderRadius: 6, cursor: 'pointer', background: active ? '#1e293b' : 'transparent', borderColor: active ? '#6366f1' : '#334155', color: active ? '#a5b4fc' : '#475569' }}>
                {active ? <Eye style={{ width: 9, height: 9 }} /> : <EyeOff style={{ width: 9, height: 9 }} />}
                {label}
              </button>
            );
          })}
        </div>
        {graph.geometry && (
          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
            <GeoBadge label="Shape" value={graph.geometry.shape} />
            <GeoBadge label="Entropy" value={graph.geometry.entropy.toFixed(2)} />
            <GeoBadge label="Polar" value={graph.geometry.polarization.toFixed(2)} />
            <GeoBadge label="Frag" value={graph.geometry.fragmentation.toFixed(2)} />
          </div>
        )}
      </div>

      {/* Cascade stop */}
      {cascadeActive && (
        <div style={{ position: 'absolute', top: 8, right: 8, zIndex: 10 }}>
          <button onClick={stopCascade} style={pillBtnStyle('#ef4444')}>Stop Cascade</button>
        </div>
      )}

      {/* Canvas layers */}
      <canvas ref={nebulaRef} style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', pointerEvents: 'none', zIndex: 0 }} />
      <div ref={containerRef} style={{ width: '100%', height: '100%', position: 'relative', zIndex: 1 }} />
      <canvas ref={edgeOverlayRef} style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', pointerEvents: 'none', zIndex: 2 }} />

      {/* Tooltip */}
      {tooltip && (
        <div style={{ position: 'absolute', left: tooltip.x + 16, top: tooltip.y - 10, zIndex: 20, background: '#0f172a', border: '1px solid #1e293b', borderRadius: 10, padding: 10, minWidth: 200, pointerEvents: 'none', boxShadow: '0 4px 24px rgba(0,0,0,0.5)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
            <div style={{ width: 10, height: 10, borderRadius: '50%', background: nodeColor(tooltip.node) }} />
            <span style={{ fontSize: 13, fontWeight: 600 }}>{hl(tooltip.node.code)}</span>
            <span style={{ fontSize: 10, color: '#64748b', fontFamily: 'monospace' }}>{tooltip.node.code}</span>
          </div>
          {tooltip.node.kind === 'bind' ? (
            <div style={{ fontSize: 10 }}>
              <div><Lbl style={{ color: '#64748b' }}>Type:</Lbl> Behavioral Composite</div>
              <div><Lbl style={{ color: '#64748b' }}>State:</Lbl> {stateIcon(tooltip.node.state)} {tooltip.node.state}</div>
              <div style={{ color: '#475569', fontStyle: 'italic', marginTop: 4, fontSize: 9 }}>
                Computed from constituent signals
              </div>
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2px 12px', fontSize: 10 }}>
              <span><Lbl style={{ color: '#64748b' }}>Kind:</Lbl> {tooltip.node.kind}</span>
              <span><Lbl style={{ color: '#64748b' }}>Type:</Lbl> {tooltip.node.type}</span>
              <span><Lbl style={{ color: '#64748b' }}>State:</Lbl> {stateIcon(tooltip.node.state)} {tooltip.node.state}</span>
              <span><Lbl style={{ color: '#64748b' }}>Region:</Lbl> {tooltip.node.region || '\u2014'}</span>
              <span><Lbl style={{ color: '#64748b' }}>Weight:</Lbl> {tooltip.node.weight}{'\u00D7'}</span>
              <span><Lbl style={{ color: '#64748b' }}>Centrality:</Lbl> {tooltip.node.betweenness.toFixed(2)}</span>
              {tooltip.node.tauMin != null && <span style={{ gridColumn: '1/3' }}><Lbl style={{ color: '#64748b' }}>Tau:</Lbl> {tooltip.node.tauMin}{'\u2013'}{tooltip.node.tauMax}ms</span>}
              {tooltip.node.plasticity && <span style={{ gridColumn: '1/3' }}><Lbl style={{ color: '#64748b' }}>Plasticity:</Lbl> {tooltip.node.plasticity}</span>}
            </div>
          )}
        </div>
      )}

      {/* Selected node detail */}
      {focusedNodeData && (
        <div style={{ position: 'absolute', top: 8, right: 8, zIndex: 20, width: 240, background: '#0f172a', border: '1px solid #1e293b', borderRadius: 12, padding: 10, boxShadow: '0 4px 24px rgba(0,0,0,0.5)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
            <div>
              <div style={{ fontSize: 13, fontWeight: 600 }}>{hl(focusedNodeData.code)}</div>
              <div style={{ fontSize: 9, color: '#64748b', fontFamily: 'monospace' }}>{focusedNodeData.id}</div>
            </div>
            <div style={{ width: 10, height: 10, borderRadius: '50%', background: nodeColor(focusedNodeData) }} />
          </div>
          {focusedNodeData.kind === 'bind' ? (
            <div style={{ fontSize: 10, marginBottom: 6 }}>
              <div><Lbl style={{ color: '#64748b' }}>Type:</Lbl> Behavioral Composite</div>
              <div><Lbl style={{ color: '#64748b' }}>State:</Lbl> {stateIcon(focusedNodeData.state)} {focusedNodeData.state}</div>
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2px 8px', fontSize: 10, marginBottom: 6 }}>
              <span><Lbl style={{ color: '#64748b' }}>Kind:</Lbl> {focusedNodeData.kind}</span>
              <span><Lbl style={{ color: '#64748b' }}>Type:</Lbl> {focusedNodeData.type}</span>
              <span><Lbl style={{ color: '#64748b' }}>State:</Lbl> {stateIcon(focusedNodeData.state)} {focusedNodeData.state}</span>
              <span><Lbl style={{ color: '#64748b' }}>Region:</Lbl> {focusedNodeData.region || '\u2014'}</span>
              <span><Lbl style={{ color: '#64748b' }}>Weight:</Lbl> {focusedNodeData.weight}{'\u00D7'}</span>
              <span><Lbl style={{ color: '#64748b' }}>Centrality:</Lbl> {focusedNodeData.betweenness.toFixed(2)}</span>
            </div>
          )}
          <div style={{ display: 'flex', gap: 4 }}>
            <button onClick={() => runCascade(focusedNodeData.id)} style={pillBtnStyle('#6366f1')}>Cascade</button>
            <button onClick={() => onFocus({ type: 'clear' })} style={pillBtnStyle('#334155')}>Close</button>
          </div>
        </div>
      )}

      {/* Legend */}
      <div style={{ position: 'absolute', bottom: 6, left: 8, zIndex: 10, display: 'flex', gap: 12, fontSize: 9, color: '#64748b' }}>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <svg width="14" height="4"><line x1="0" y1="2" x2="14" y2="2" stroke="#3b82f6" strokeWidth="2" /></svg> Causal
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <svg width="14" height="8"><path d="M0,6 Q7,0 14,6" fill="none" stroke="#f59e0b" strokeWidth="1.5" /></svg> Feedback
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <svg width="14" height="4"><line x1="0" y1="2" x2="14" y2="2" stroke="#ef4444" strokeWidth="2" strokeDasharray="4 3" /></svg> Dysreg
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <svg width="14" height="4"><line x1="0" y1="2" x2="14" y2="2" stroke="#ec4899" strokeWidth="1.5" strokeDasharray="2 2" /></svg> Bind
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <svg width="14" height="4"><line x1="0" y1="2" x2="14" y2="2" stroke="#f59e0b" strokeWidth="3" strokeDasharray="8 4" /></svg> Compensator
        </span>
      </div>
    </div>
  );
}
