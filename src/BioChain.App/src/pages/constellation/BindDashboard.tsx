import { useMemo } from 'react';
import type { ConstellationGraphResponse, ConstellationAnalysisResponse, ConstellationNode } from '@/types/constellation';
import { TYPE_COLORS, stateIcon, humanLabel, formatBindName } from '@/types/constellation';
import type { FocusTarget, FocusState } from '@/stores/constellationStore';

interface BindDashboardProps {
  graph: ConstellationGraphResponse;
  analysis: ConstellationAnalysisResponse | null;
  focus: FocusState;
  onFocus: (target: FocusTarget) => void;
}

interface BindInfo {
  node: ConstellationNode;
  contributors: { node: ConstellationNode; operator: string; weight: number }[];
  fragility: 'stable' | 'costly' | 'brittle' | 'unstable';
}

export function BindDashboard({ graph, analysis, focus, onFocus }: BindDashboardProps) {
  const binds = useMemo(() => {
    const bindNodes = graph.nodes.filter((n) => n.kind === 'bind');

    // Find compensators that touch bind contributors
    const compensatorNodes = new Set<string>();
    (analysis?.compensators ?? []).forEach((c) => c.nodes.forEach((n) => compensatorNodes.add(n)));

    // Find dysreg cascade affected nodes
    const dysregAffected = new Set<string>();
    graph.dysregCascades.forEach((d) => d.affectedPath.forEach((n) => dysregAffected.add(n)));

    return bindNodes.map((bind): BindInfo => {
      const bindEdges = graph.edges.filter((e) => e.target === bind.id && e.operatorClass === 'bind');
      const contributors = bindEdges
        .map((e) => {
          const node = graph.nodes.find((n) => n.id === e.source);
          return node ? { node, operator: e.operator, weight: node.weight } : null;
        })
        .filter((c): c is NonNullable<typeof c> => c !== null)
        .sort((a, b) => b.weight - a.weight);

      // Compute fragility
      const contributorCodes = contributors.map((c) => c.node.code);
      const compensated = contributorCodes.filter((c) => compensatorNodes.has(c)).length;
      const dysreg = contributorCodes.filter((c) => dysregAffected.has(c)).length;
      const fragScore = compensated * 2 + dysreg;

      const fragility: BindInfo['fragility'] =
        fragScore === 0 ? 'stable'
        : fragScore <= 2 ? 'costly'
        : fragScore <= 4 ? 'brittle'
        : 'unstable';

      return { node: bind, contributors, fragility };
    });
  }, [graph, analysis]);

  const fragilityColors: Record<string, string> = {
    stable: '#22c55e', costly: '#f59e0b', brittle: '#f97316', unstable: '#ef4444',
  };

  if (binds.length === 0) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#475569', fontSize: 11 }}>
        No behavioral composites detected
      </div>
    );
  }

  return (
    <div style={{ padding: 8, display: 'flex', flexDirection: 'column', gap: 6 }}>
      {binds.map((bind) => {
        const isFocused = focus.bind === bind.node.id || focus.entity === bind.node.id;
        const fc = fragilityColors[bind.fragility];
        const maxWeight = Math.max(...bind.contributors.map((c) => c.weight), 1);

        return (
          <div
            key={bind.node.id}
            onClick={() => onFocus({ type: 'bind', id: bind.node.id })}
            style={{
              padding: 0, borderRadius: 10, background: '#0f172a',
              border: `1px solid ${isFocused ? '#ec4899' : '#1e293b'}`,
              cursor: 'pointer', overflow: 'hidden',
            }}
          >
            <div style={{ padding: 8 }}>
              {/* Header */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                <span style={{ fontSize: 12, fontWeight: 700 }}>
                  {formatBindName(bind.node.code)}
                </span>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 10, color: '#94a3b8' }}>
                    {stateIcon(bind.node.state)} {bind.node.state}
                  </span>
                </div>
              </div>

              {/* Contribution bars */}
              {bind.contributors.length > 0 && (
                <div style={{ marginBottom: 6 }}>
                  <div style={{ fontSize: 8, fontWeight: 700, color: '#475569', textTransform: 'uppercase', marginBottom: 3 }}>
                    Contributors ({bind.contributors.length})
                  </div>
                  {bind.contributors.slice(0, 8).map((c, i) => {
                    const pct = (c.weight / maxWeight) * 100;
                    const color = TYPE_COLORS[c.node.type] ?? '#64748b';
                    return (
                      <div
                        key={i}
                        onClick={(e) => { e.stopPropagation(); onFocus({ type: 'entity', id: c.node.id }); }}
                        style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 2, cursor: 'pointer' }}
                      >
                        <span style={{ fontSize: 8, fontFamily: 'monospace', color: color, width: 60, flexShrink: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {humanLabel(c.node.code, analysis?.humanLabels)}
                        </span>
                        <div style={{ flex: 1, height: 6, background: '#1e293b', borderRadius: 3, overflow: 'hidden' }}>
                          <div style={{ width: `${pct}%`, height: '100%', background: color, borderRadius: 3, transition: 'width 0.3s' }} />
                        </div>
                        <span style={{ fontSize: 8, color: '#64748b', width: 24, textAlign: 'right', flexShrink: 0 }}>
                          {c.weight}{'\u00D7'}
                        </span>
                        <span style={{ fontSize: 8, color: '#94a3b8', flexShrink: 0 }}>
                          {stateIcon(c.node.state)}
                        </span>
                      </div>
                    );
                  })}
                  {bind.contributors.length > 8 && (
                    <div style={{ fontSize: 8, color: '#475569', marginTop: 2 }}>+{bind.contributors.length - 8} more</div>
                  )}
                </div>
              )}

              {/* Fragility */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ fontSize: 8, fontWeight: 700, color: '#475569', textTransform: 'uppercase' }}>Fragility</span>
                <span style={{
                  fontSize: 8, fontWeight: 700, padding: '1px 6px', borderRadius: 4,
                  background: fc + '20', color: fc, textTransform: 'uppercase',
                }}>
                  {bind.fragility}
                </span>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
