import { useMemo } from 'react';
import type { ConstellationGraphResponse, ConstellationAnalysisResponse, ConstellationNode } from '@/types/constellation';
import { TYPE_COLORS, stateIcon, humanLabel, formatBindName } from '@/types/constellation';
import { NodeChip } from './shared/NodeChip';
import { StatusBadge } from './shared/StatusBadge';
import { SeverityBadge } from './shared/SeverityBadge';
import { useConstellationStore } from '@/stores/constellationStore';
import type { FocusTarget, FocusState, PanelTab } from '@/stores/constellationStore';

interface ExplanationCoreProps {
  graph: ConstellationGraphResponse;
  analysis: ConstellationAnalysisResponse | null;
  analysisLoading: boolean;
  analysisError: string | null;
  focus: FocusState;
  activeTab: PanelTab;
  onFocus: (target: FocusTarget) => void;
}

const card: React.CSSProperties = {
  padding: 10, borderRadius: 10, background: '#0f172a',
  border: '1px solid #1e293b', marginBottom: 8,
};

const COMMUNITY_STATUS_COLORS: Record<string, string> = {
  functional: '#22c55e', compensated: '#f59e0b', impaired: '#f97316',
  dysfunctional: '#ef4444', collapsed: '#7f1d1d',
};

export function ExplanationCore({ graph, analysis, analysisLoading, analysisError, focus, activeTab, onFocus }: ExplanationCoreProps) {
  const hl = (code: string) => humanLabel(code, analysis?.humanLabels);

  // Structured drill data for focused entity
  const drill = useMemo(() => {
    if (!focus.entity) return null;

    const node = graph.nodes.find((n) => n.id === focus.entity);
    if (!node) return null;

    const upstream = graph.edges
      .filter((e) => e.target === focus.entity)
      .map((e) => ({ node: graph.nodes.find((n) => n.id === e.source)!, operator: e.operator, cls: e.operatorClass }))
      .filter((u) => u.node);

    const downstream = graph.edges
      .filter((e) => e.source === focus.entity)
      .map((e) => ({ node: graph.nodes.find((n) => n.id === e.target)!, operator: e.operator, cls: e.operatorClass }))
      .filter((d) => d.node);

    const gatingEdges = graph.edges.filter((e) =>
      (e.source === focus.entity || e.target === focus.entity) &&
      graph.nodes.find((n) => n.id === (e.source === focus.entity ? e.target : e.source))?.kind === 'gate'
    );
    const gates = gatingEdges.map((e) => {
      const gateId = e.source === focus.entity ? e.target : e.source;
      return graph.nodes.find((n) => n.id === gateId);
    }).filter((g): g is ConstellationNode => g !== null);

    const loops = graph.feedbackLoops.filter((l) =>
      l.loopPath.some((p) => p === node.code || p === focus.entity)
    );

    const compensators = (analysis?.compensators ?? []).filter((c) =>
      c.nodes.includes(node.code)
    );

    return { node, upstream, downstream, gates, loops, compensators };
  }, [focus.entity, graph, analysis]);

  // Show structured drill if entity is focused
  if (drill) {
    const color = TYPE_COLORS[drill.node.type] ?? '#94a3b8';

    return (
      <div style={{ padding: 10 }}>
        {/* Entity header */}
        <div style={{ ...card, borderLeft: `3px solid ${color}` }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
            <div style={{ width: 12, height: 12, borderRadius: '50%', background: color }} />
            <div>
              <div style={{ fontSize: 14, fontWeight: 700 }}>{hl(drill.node.code)}</div>
              <div style={{ fontSize: 10, color: '#64748b', fontFamily: 'monospace' }}>{drill.node.id}</div>
            </div>
            <span style={{ marginLeft: 'auto', fontSize: 12, color }}>{stateIcon(drill.node.state)} {drill.node.state}</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2px 10px', fontSize: 10 }}>
            <span style={{ color: '#64748b' }}>Kind: <span style={{ color: '#94a3b8' }}>{drill.node.kind}</span></span>
            <span style={{ color: '#64748b' }}>Type: <span style={{ color: '#94a3b8' }}>{drill.node.type}</span></span>
            <span style={{ color: '#64748b' }}>Region: <span style={{ color: '#94a3b8' }}>{drill.node.region || '\u2014'}</span></span>
            <span style={{ color: '#64748b' }}>Weight: <span style={{ color: '#94a3b8' }}>{drill.node.weight}{'\u00D7'}</span></span>
            <span style={{ color: '#64748b' }}>Centrality: <span style={{ color: '#94a3b8' }}>{drill.node.betweenness.toFixed(2)}</span></span>
            <span style={{ color: '#64748b' }}>Community: <span style={{ color: '#94a3b8' }}>{drill.node.community}</span></span>
          </div>
        </div>

        {/* Upstream */}
        {drill.upstream.length > 0 && (
          <Section title={`UPSTREAM (${drill.upstream.length} causes)`} color="#3b82f6">
            {drill.upstream.map((u, i) => (
              <EdgeRow key={i} node={u.node} operator={u.operator} cls={u.cls} direction="in" hl={hl} onFocus={onFocus} />
            ))}
          </Section>
        )}

        {/* Downstream */}
        {drill.downstream.length > 0 && (
          <Section title={`DOWNSTREAM (${drill.downstream.length} effects)`} color="#22c55e">
            {drill.downstream.map((d, i) => (
              <EdgeRow key={i} node={d.node} operator={d.operator} cls={d.cls} direction="out" hl={hl} onFocus={onFocus} />
            ))}
          </Section>
        )}

        {/* Gates */}
        {drill.gates.length > 0 && (
          <Section title={`GATING (${drill.gates.length} gates)`} color="#f59e0b">
            {drill.gates.map((g) => (
              <div
                key={g.id}
                onClick={() => onFocus({ type: 'gate', id: g.id })}
                style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '3px 0', cursor: 'pointer', fontSize: 10 }}
              >
                <span style={{ color: '#f59e0b' }}>{'\u25C6'}</span>
                <span style={{ fontFamily: 'monospace', color: '#e2e8f0' }}>{g.code}</span>
                <span style={{ color: '#64748b' }}>{stateIcon(g.state)} {g.state}</span>
              </div>
            ))}
          </Section>
        )}

        {/* Feedback Loops */}
        {drill.loops.length > 0 && (
          <Section title={`FEEDBACK LOOPS (${drill.loops.length})`} color="#8b5cf6">
            {drill.loops.map((l, i) => (
              <div key={i} style={{ fontSize: 10, padding: '3px 0' }}>
                <span style={{ color: l.isPositive ? '#22c55e' : '#ef4444', marginRight: 6 }}>
                  {l.isPositive ? '+' : '\u2212'}
                </span>
                <span style={{ fontFamily: 'monospace', color: '#94a3b8' }}>
                  {l.loopPath.join(' \u2192 ')}
                </span>
              </div>
            ))}
          </Section>
        )}

        {/* Compensators */}
        {drill.compensators.length > 0 && (
          <Section title={`COMPENSATORS (${drill.compensators.length})`} color="#f97316">
            {drill.compensators.map((c) => (
              <div key={c.id} style={{ fontSize: 10, padding: '3px 0', lineHeight: 1.4 }}>
                <div style={{ color: '#e2e8f0', fontWeight: 600 }}>{c.what}</div>
                <div style={{ color: '#64748b' }}>Masking: {c.masking}</div>
                <div style={{ color: '#64748b' }}>Cost: {c.cost}</div>
              </div>
            ))}
          </Section>
        )}

        <button
          onClick={() => onFocus({ type: 'clear' })}
          style={{ marginTop: 8, padding: '4px 12px', fontSize: 10, fontWeight: 600, background: '#1e293b', color: '#94a3b8', border: '1px solid #334155', borderRadius: 8, cursor: 'pointer' }}
        >
          Clear Focus
        </button>
      </div>
    );
  }

  // Default: show analysis panels (same as old sidebar)
  if (analysisLoading && !analysis) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 8 }}>
        <div style={{ width: 20, height: 20, border: '2px solid #64748b', borderTopColor: 'transparent', borderRadius: '50%', animation: 'spin 1s linear infinite' }} />
        <span style={{ fontSize: 12, color: '#64748b' }}>Generating deep analysis...</span>
        <span style={{ fontSize: 10, color: '#475569' }}>This may take 10-30 seconds</span>
      </div>
    );
  }

  if (analysisError) {
    return <div style={{ padding: 10, fontSize: 12, color: '#ef4444' }}>{analysisError}</div>;
  }

  if (!analysis) {
    return <div style={{ padding: 10, fontSize: 11, color: '#475569' }}>Waiting for analysis data...</div>;
  }

  // Render tab-based content
  return (
    <div style={{ padding: 10 }}>
      {activeTab === 'systems' && <SystemsPanel analysis={analysis} graph={graph} hl={hl} onFocus={onFocus} />}
      {activeTab === 'person' && <PersonPanel analysis={analysis} hl={hl} onFocus={onFocus} />}
      {activeTab === 'architecture' && <ArchitecturePanel analysis={analysis} hl={hl} />}
      {activeTab === 'whatif' && <WhatIfPanel analysis={analysis} graph={graph} hl={hl} onFocus={onFocus} />}
    </div>
  );
}

// ── Sub-panels ──────────────────────────────────────────────

function Section({ title, color, children }: { title: string; color: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 8 }}>
      <div style={{ fontSize: 9, fontWeight: 700, color, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 4 }}>
        <span style={{ width: 8, height: 1, background: color }} />
        {title}
      </div>
      {children}
    </div>
  );
}

function EdgeRow({ node, operator, cls, direction, hl, onFocus }: {
  node: ConstellationNode; operator: string; cls: string; direction: 'in' | 'out';
  hl: (code: string) => string; onFocus: (t: FocusTarget) => void;
}) {
  const color = TYPE_COLORS[node.type] ?? '#64748b';
  return (
    <div
      onClick={() => onFocus({ type: 'entity', id: node.id })}
      style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '2px 0', cursor: 'pointer', fontSize: 10 }}
    >
      <span style={{ color: '#475569', fontSize: 9 }}>{direction === 'in' ? '\u2190' : '\u2192'}</span>
      <span style={{ color, fontFamily: 'monospace' }}>{hl(node.code)}</span>
      <span style={{ color: '#475569', fontSize: 8 }}>({operator})</span>
      <span style={{ color: '#64748b' }}>{stateIcon(node.state)}</span>
    </div>
  );
}

function SystemsPanel({ analysis, graph, hl, onFocus }: {
  analysis: ConstellationAnalysisResponse; graph: ConstellationGraphResponse;
  hl: (code: string) => string; onFocus: (t: FocusTarget) => void;
}) {
  return (
    <div>
      {(analysis.communities ?? []).map((c) => {
        const statusColor = COMMUNITY_STATUS_COLORS[c.status.toLowerCase()] ?? '#64748b';
        const graphCom = graph.communities.find((gc) => gc.id === c.id);
        const memberNodes = graphCom
          ? graphCom.nodes.map((nid) => graph.nodes.find((n) => n.id === nid)).filter(Boolean) as ConstellationNode[]
          : [];
        const signalNodes = memberNodes.filter((n) => n.kind === 'signal');

        return (
          <div key={c.id} style={{ ...card, padding: 0, overflow: 'hidden', cursor: 'pointer' }} onClick={() => onFocus({ type: 'community', id: c.id })}>
            <div style={{ height: 3, background: statusColor + '30' }}>
              <div style={{ height: '100%', background: statusColor, width: c.status === 'functional' ? '100%' : c.status === 'compensated' ? '70%' : c.status === 'impaired' ? '45%' : c.status === 'dysfunctional' ? '25%' : '10%', borderRadius: '0 2px 2px 0' }} />
            </div>
            <div style={{ padding: 8 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <span style={{ fontSize: 12, fontWeight: 700 }}>{c.name}</span>
                <StatusBadge status={c.status} />
              </div>
              <p style={{ fontSize: 10, color: '#94a3b8', margin: '0 0 6px', lineHeight: 1.4 }}>{c.summary}</p>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4, marginBottom: 6 }}>
                <div style={{ padding: 6, background: '#22c55e08', border: '1px solid #22c55e20', borderRadius: 6 }}>
                  <div style={{ fontSize: 7, fontWeight: 700, color: '#22c55e', textTransform: 'uppercase', marginBottom: 3 }}>When Working</div>
                  <div style={{ fontSize: 9, color: '#94a3b8', lineHeight: 1.3 }}>{c.whenWorking}</div>
                </div>
                <div style={{ padding: 6, background: '#ef444408', border: '1px solid #ef444420', borderRadius: 6 }}>
                  <div style={{ fontSize: 7, fontWeight: 700, color: '#ef4444', textTransform: 'uppercase', marginBottom: 3 }}>When Broken</div>
                  <div style={{ fontSize: 9, color: '#94a3b8', lineHeight: 1.3 }}>{c.whenBroken}</div>
                </div>
              </div>
              {signalNodes.length > 0 && (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, marginBottom: 4 }}>
                  {signalNodes.slice(0, 8).map((n) => (
                    <span key={n.id} style={{ padding: '1px 5px', borderRadius: 3, fontSize: 8, fontWeight: 600, background: (TYPE_COLORS[n.type] ?? '#64748b') + '20', color: TYPE_COLORS[n.type] ?? '#94a3b8' }}>
                      {hl(n.code)} {stateIcon(n.state)}
                    </span>
                  ))}
                </div>
              )}
              {c.fix && c.fix.length > 0 && (
                <div>
                  <div style={{ fontSize: 7, fontWeight: 700, color: '#6366f1', textTransform: 'uppercase', marginBottom: 3 }}>Fix Protocol</div>
                  {c.fix.map((f, i) => (
                    <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 9, marginBottom: 2 }}>
                      <span style={{ width: 14, height: 14, borderRadius: '50%', background: '#6366f115', color: '#a5b4fc', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 7, fontWeight: 700, flexShrink: 0 }}>{i + 1}</span>
                      <span style={{ color: '#e2e8f0' }}>{f.action}</span>
                      <span style={{ color: '#6366f1' }}>{'\u2192'}</span>
                      <span style={{ color: '#94a3b8' }}>{f.target}</span>
                      <SeverityBadge severity={f.priority} />
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        );
      })}
      {(analysis.motifs ?? []).length > 0 && (
        <>
          <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase', margin: '8px 0 4px' }}>Graph Motifs</div>
          {analysis.motifs!.map((m) => (
            <div key={m.id} style={card}>
              <div style={{ fontSize: 11, fontWeight: 600, marginBottom: 2 }}>{m.name}</div>
              <div style={{ fontSize: 9, fontFamily: 'monospace', color: '#8b5cf6', marginBottom: 3 }}>{m.pattern}</div>
              <p style={{ fontSize: 10, color: '#94a3b8', margin: '3px 0' }}>{m.meaning}</p>
              {m.instances.map((inst, i) => (
                <div key={i} style={{ fontSize: 9, color: '#64748b' }}>
                  {inst.path.join(' \u2192 ')} <span style={{ color: '#94a3b8' }}>({inst.label})</span>
                </div>
              ))}
            </div>
          ))}
        </>
      )}
    </div>
  );
}

function PersonPanel({ analysis, hl, onFocus }: {
  analysis: ConstellationAnalysisResponse; hl: (code: string) => string; onFocus: (t: FocusTarget) => void;
}) {
  return (
    <div>
      {(analysis.narratives ?? []).length > 0 && (
        <>
          <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase', marginBottom: 4 }}>Narratives</div>
          {analysis.narratives!.map((n) => (
            <div key={n.id} style={card}>
              <div style={{ fontSize: 11, fontWeight: 600, marginBottom: 2 }}>{n.title}</div>
              <div style={{ fontSize: 9, fontFamily: 'monospace', color: '#6366f1', marginBottom: 3 }}>{n.formula}</div>
              <p style={{ fontSize: 10, color: '#94a3b8', margin: '3px 0' }}>{n.text}</p>
              <div style={{ display: 'flex', gap: 10, fontSize: 9, color: '#64748b', marginBottom: 3 }}>
                <span>Load: {(n.load * 100).toFixed(0)}%</span>
                <span>Control: {(n.controlEffort * 100).toFixed(0)}%</span>
                <span>Fragility: {(n.fragility * 100).toFixed(0)}%</span>
              </div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
                {n.nodes.map((code) => <NodeChip key={code} code={code} label={hl(code)} />)}
              </div>
            </div>
          ))}
        </>
      )}
      {(analysis.contradictions ?? []).length > 0 && (
        <>
          <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase', margin: '8px 0 4px' }}>Contradictions</div>
          {analysis.contradictions!.map((c) => (
            <div key={c.id} style={card}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3 }}>
                <span style={{ fontSize: 11, fontWeight: 600, color: '#f59e0b' }}>Contradiction</span>
                <span style={{ fontSize: 9, color: '#64748b' }}>Tension: {(c.tension * 100).toFixed(0)}%</span>
              </div>
              {c.surface.map((s, i) => <div key={i} style={{ fontSize: 10, color: '#94a3b8' }}>{'\u2022'} {s}</div>)}
              <div style={{ fontSize: 10, color: '#38bdf8', marginTop: 3 }}>{c.resolution}</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, marginTop: 3 }}>
                {c.nodes.map((code) => <NodeChip key={code} code={code} label={hl(code)} />)}
              </div>
            </div>
          ))}
        </>
      )}
      {(analysis.compensators ?? []).length > 0 && (
        <>
          <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase', margin: '8px 0 4px' }}>Compensators</div>
          {analysis.compensators!.map((c) => (
            <div key={c.id} style={card}>
              <div style={{ fontSize: 11, fontWeight: 600, marginBottom: 2 }}>{c.what}</div>
              <div style={{ fontSize: 10, color: '#94a3b8' }}>
                <div><b style={{ color: '#e2e8f0' }}>Masking:</b> {c.masking}</div>
                <div><b style={{ color: '#e2e8f0' }}>Cost:</b> {c.cost}</div>
                <div><b style={{ color: '#e2e8f0' }}>Fragility:</b> {c.fragility}</div>
              </div>
              <div style={{ fontSize: 9, color: '#64748b', marginTop: 3 }}>Cost: {(c.costScore * 100).toFixed(0)}%</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, marginTop: 3 }}>
                {c.nodes.map((code) => <NodeChip key={code} code={code} label={hl(code)} />)}
              </div>
            </div>
          ))}
        </>
      )}
    </div>
  );
}

function ArchitecturePanel({ analysis, hl }: { analysis: ConstellationAnalysisResponse; hl: (code: string) => string }) {
  return (
    <div>
      <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase', marginBottom: 4 }}>If This Continues...</div>
      {(analysis.architecture ?? []).map((a) => (
        <div key={a.id} style={card}>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3 }}>
            <span style={{ fontSize: 11, fontWeight: 600 }}>{a.title}</span>
            <SeverityBadge severity={a.severity} />
          </div>
          <div style={{ fontSize: 9, color: '#64748b', marginBottom: 3 }}>{a.frame}</div>
          <p style={{ fontSize: 10, color: '#94a3b8', margin: '3px 0' }}>{a.text}</p>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, marginTop: 3 }}>
            {a.nodes.map((code) => <NodeChip key={code} code={code} label={hl(code)} />)}
          </div>
        </div>
      ))}
    </div>
  );
}

function WhatIfPanel({ analysis, graph, hl, onFocus }: {
  analysis: ConstellationAnalysisResponse; graph: ConstellationGraphResponse;
  hl: (code: string) => string; onFocus: (t: FocusTarget) => void;
}) {
  const selectedIntervention = useConstellationStore((s) => s.selectedIntervention);
  const setSelectedIntervention = useConstellationStore((s) => s.setSelectedIntervention);
  const pertEntries = analysis.perturbations ? Object.entries(analysis.perturbations) : [];
  const selectedP = selectedIntervention && analysis.perturbations ? analysis.perturbations[selectedIntervention] : null;

  return (
    <div>
      <div style={{ fontSize: 8, fontWeight: 700, color: '#475569', textTransform: 'uppercase', marginBottom: 4 }}>Select Intervention</div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3, marginBottom: 10 }}>
        {pertEntries.map(([key]) => {
          const node = graph.nodes.find((n) => n.code === key);
          const color = node ? TYPE_COLORS[node.type] ?? '#6366f1' : '#6366f1';
          const isActive = selectedIntervention === key;
          return (
            <button key={key} onClick={() => setSelectedIntervention(isActive ? null : key)} style={{ padding: '3px 8px', borderRadius: 6, border: '1px solid', cursor: 'pointer', fontSize: 9, fontWeight: 600, background: isActive ? color + '25' : 'transparent', borderColor: isActive ? color : '#334155', color: isActive ? color : '#64748b' }}>
              {hl(key)}
            </button>
          );
        })}
      </div>
      {selectedP && (
        <>
          <div style={{ padding: 8, borderRadius: 8, marginBottom: 8, background: '#6366f108', border: '1px solid #6366f120' }}>
            <div style={{ fontSize: 7, fontWeight: 700, color: '#6366f1', textTransform: 'uppercase', marginBottom: 4 }}>Graph-Grounded Analysis</div>
            <p style={{ fontSize: 10, color: '#c4b5fd', margin: 0, lineHeight: 1.4 }}>{selectedP.llm}</p>
          </div>
          <div style={{ fontSize: 7, fontWeight: 700, color: '#475569', textTransform: 'uppercase', marginBottom: 4 }}>Affected Nodes ({selectedP.targets.length})</div>
          {selectedP.targets.map((t, i) => {
            const targetNode = graph.nodes.find((n) => n.code === t.node);
            const tColor = targetNode ? TYPE_COLORS[targetNode.type] ?? '#94a3b8' : '#94a3b8';
            return (
              <div key={i} style={{ ...card, borderLeft: `3px solid ${tColor}` }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontSize: 11, fontWeight: 700, fontFamily: 'monospace' }}>{hl(t.node)}</span>
                  <span style={{ fontSize: 11, fontWeight: 700, color: t.delta.includes('+') || t.delta.includes('\u2191') ? '#22c55e' : t.delta.includes('-') || t.delta.includes('\u2193') ? '#ef4444' : '#f59e0b' }}>{t.delta}</span>
                </div>
                <div style={{ fontSize: 9, color: '#64748b', marginTop: 2 }}>Delay: {t.delay}</div>
                <div style={{ fontSize: 9, color: '#94a3b8', marginTop: 2, lineHeight: 1.3 }}>{t.mechanism}</div>
              </div>
            );
          })}
        </>
      )}
      {!selectedP && pertEntries.length > 0 && <div style={{ textAlign: 'center', padding: 16, color: '#475569', fontSize: 10 }}>Select an intervention above</div>}
      {pertEntries.length === 0 && <div style={{ textAlign: 'center', padding: 16, color: '#475569', fontSize: 10 }}>No perturbation data available</div>}
    </div>
  );
}
