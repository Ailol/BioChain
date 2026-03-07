import { useMemo } from 'react';
import type { ConstellationGraphResponse, ConstellationAnalysisResponse } from '@/types/constellation';
import { TYPE_COLORS, stateIcon, humanLabel } from '@/types/constellation';
import type { FocusTarget, FocusState } from '@/stores/constellationStore';

interface PhaseCorridorProps {
  graph: ConstellationGraphResponse;
  analysis: ConstellationAnalysisResponse | null;
  focus: FocusState;
  onFocus: (target: FocusTarget) => void;
}

// Derive phase approximations from signal states + cascade data
// This is a heuristic until the backend provides actual phase data
const PHASE_LABELS = ['baseline', 'acute', 'sustained', 'compensated', 'chronic'];

const STATE_SEVERITY: Record<string, number> = {
  homeostatic: 0, '\u2248': 0, active: 1,
  elevated: 2, '\u2B06': 2, depleted: -2, '\u2B07': -2,
  '\u2B06\u2B06': 3, '\u2B07\u2B07': -3,
  desens: -1, upreg: 1, primed: 1,
};

const severityColor = (sev: number): string => {
  if (sev === 0) return '#22c55e';
  if (Math.abs(sev) === 1) return '#f59e0b';
  if (Math.abs(sev) === 2) return '#f97316';
  return '#ef4444';
};

export function PhaseCorridor({ graph, analysis, focus, onFocus }: PhaseCorridorProps) {
  // Get top signals sorted by weight
  const signals = useMemo(() => {
    return graph.nodes
      .filter((n) => n.kind === 'signal')
      .sort((a, b) => b.weight - a.weight)
      .slice(0, 15);
  }, [graph]);

  // Derive phase data heuristically
  // In the absence of real phase data from the backend, we approximate:
  // - baseline: homeostatic state
  // - acute: current state (from analysis)
  // - sustained: dampened version of acute
  // - compensated: if compensators exist, show compensation effect
  // - chronic: if dysreg cascades exist, show chronic deviation
  const phaseData = useMemo(() => {
    const compensatedCodes = new Set<string>();
    (analysis?.compensators ?? []).forEach((c) => c.nodes.forEach((n) => compensatedCodes.add(n)));

    const dysregCodes = new Set<string>();
    graph.dysregCascades.forEach((d) => d.affectedPath.forEach((n) => dysregCodes.add(n)));

    return signals.map((signal) => {
      const sev = STATE_SEVERITY[signal.state] ?? 0;
      const isCompensated = compensatedCodes.has(signal.code);
      const isDysreg = dysregCodes.has(signal.code);

      return {
        signal,
        phases: {
          baseline: '\u2248',
          acute: signal.state,
          sustained: sev > 0 ? '\u2B06' : sev < 0 ? '\u2B07' : '\u2248',
          compensated: isCompensated ? '\u2248' : (sev > 0 ? '\u2B06' : sev < 0 ? '\u2B07' : '\u2248'),
          chronic: isDysreg ? (sev > 0 ? '\u2B06\u2B06' : '\u2B07\u2B07') : signal.state,
        },
      };
    });
  }, [signals, graph, analysis]);

  return (
    <div style={{ width: '100%', height: '100%', overflow: 'auto' }}>
      <div style={{ minWidth: 400, padding: 6 }}>
        {/* Phase headers */}
        <div style={{ display: 'grid', gridTemplateColumns: '80px repeat(5, 1fr)', gap: 1, marginBottom: 2 }}>
          <div style={{ fontSize: 8, color: '#475569', padding: '2px 4px' }}>Signal</div>
          {PHASE_LABELS.map((phase) => (
            <div key={phase} style={{
              fontSize: 7, fontWeight: 700, color: '#475569', textAlign: 'center',
              padding: '2px 4px', textTransform: 'uppercase', letterSpacing: '0.05em',
            }}>
              {phase}
            </div>
          ))}
        </div>

        {/* Signal rows */}
        {phaseData.map(({ signal, phases }) => {
          const isFocused = focus.entity === signal.id;
          const color = TYPE_COLORS[signal.type] ?? '#64748b';

          return (
            <div
              key={signal.id}
              onClick={() => onFocus({ type: 'entity', id: signal.id })}
              style={{
                display: 'grid', gridTemplateColumns: '80px repeat(5, 1fr)', gap: 1,
                background: isFocused ? color + '10' : 'transparent',
                borderRadius: 4, cursor: 'pointer',
                border: isFocused ? `1px solid ${color}30` : '1px solid transparent',
                marginBottom: 1,
              }}
            >
              {/* Signal label */}
              <div style={{
                fontSize: 9, fontFamily: 'monospace', color: color,
                padding: '3px 4px', display: 'flex', alignItems: 'center', gap: 3,
                overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
              }}>
                <span style={{ width: 4, height: 4, borderRadius: '50%', background: color, flexShrink: 0 }} />
                {humanLabel(signal.code, analysis?.humanLabels)}
              </div>

              {/* Phase cells */}
              {PHASE_LABELS.map((phase) => {
                const state = phases[phase as keyof typeof phases];
                const sev = STATE_SEVERITY[state] ?? 0;

                return (
                  <div key={phase} style={{
                    textAlign: 'center', padding: '2px 0',
                    background: severityColor(sev) + '10',
                    fontSize: 10, color: severityColor(sev),
                    borderRadius: 2,
                  }}>
                    {stateIcon(state)}
                  </div>
                );
              })}
            </div>
          );
        })}

        {/* Gate events placeholder */}
        <div style={{ marginTop: 8, padding: 4, borderTop: '1px solid #1e293b' }}>
          <div style={{ fontSize: 8, color: '#475569', textTransform: 'uppercase' }}>
            Phase data derived heuristically — backend phase assignment coming soon
          </div>
        </div>
      </div>
    </div>
  );
}
