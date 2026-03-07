import { useMemo } from 'react';
import type { ConstellationGraphResponse, ConstellationAnalysisResponse, ConstellationNode } from '@/types/constellation';
import { TYPE_COLORS, stateIcon, humanLabel } from '@/types/constellation';
import type { FocusTarget, FocusState } from '@/stores/constellationStore';

interface GateBoardProps {
  graph: ConstellationGraphResponse;
  analysis: ConstellationAnalysisResponse | null;
  focus: FocusState;
  onFocus: (target: FocusTarget) => void;
}

interface GateInput {
  node: ConstellationNode;
  operator: string;
  gain: number | null;
  delayMs: number | null;
}

interface GateInfo {
  node: ConstellationNode;
  inputs: GateInput[];
  outputs: { node: ConstellationNode; operator: string }[];
  state: string;
  loopCount: number;
  conditionFormula: string;
}

export function GateBoard({ graph, analysis, focus, onFocus }: GateBoardProps) {
  const gates = useMemo(() => {
    const gateNodes = graph.nodes.filter((n) => n.kind === 'gate');
    return gateNodes.map((gate): GateInfo => {
      const inputEdges = graph.edges.filter((e) => e.target === gate.id);
      const inputs: GateInput[] = inputEdges
        .map((e) => {
          const node = graph.nodes.find((n) => n.id === e.source);
          return node ? { node, operator: e.operator, gain: e.gain ?? null, delayMs: e.delayMs ?? null } : null;
        })
        .filter((i): i is GateInput => i !== null);

      const outputs = graph.edges
        .filter((e) => e.source === gate.id)
        .map((e) => ({ node: graph.nodes.find((n) => n.id === e.target)!, operator: e.operator }))
        .filter((o) => o.node);

      // Count feedback loops this gate participates in
      const loopCount = graph.feedbackLoops.filter((l) =>
        l.loopPath.some((p) => p === gate.code || p === gate.id)
      ).length;

      // Derive state
      const state = gate.state === 'active' ? 'active'
        : gate.state === 'latched' ? 'latched'
        : gate.state === 'armed' || gate.state === 'primed' ? 'armed'
        : gate.state === 'decaying' ? 'decaying'
        : 'inactive';

      // Build condition formula from inputs
      const conditionFormula = inputs.length > 0
        ? inputs.map((inp) => {
            const op = inp.operator.includes('+') ? '+' : inp.operator.includes('-') ? '\u2212' : '\u2022';
            return `${op} ${inp.node.code}`;
          }).join(' \u2227 ')
        : 'no inputs';

      return { node: gate, inputs, outputs, state, loopCount, conditionFormula };
    });
  }, [graph]);

  const stateColors: Record<string, string> = {
    armed: '#f59e0b', active: '#22c55e', latched: '#ef4444', decaying: '#64748b', inactive: '#334155',
  };

  const stateEmoji: Record<string, string> = {
    armed: '\uD83D\uDFE1', active: '\uD83D\uDFE2', latched: '\uD83D\uDD34', decaying: '\u26AA', inactive: '\u2B1C',
  };

  if (gates.length === 0) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#475569', fontSize: 11 }}>
        No gates detected in this profile
      </div>
    );
  }

  return (
    <div style={{ padding: 8, display: 'flex', flexDirection: 'column', gap: 6, overflow: 'auto' }}>
      {/* Summary */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 9, color: '#475569' }}>
        <span>{gates.length} gates</span>
        <span>{gates.filter((g) => g.state === 'active').length} active</span>
        <span>{gates.filter((g) => g.state === 'armed').length} armed</span>
        <span>{gates.filter((g) => g.state === 'latched').length} latched</span>
      </div>

      {gates.map((gate) => {
        const isFocused = focus.gate === gate.node.id || focus.entity === gate.node.id;
        const sc = stateColors[gate.state] ?? '#334155';
        return (
          <div
            key={gate.node.id}
            onClick={() => onFocus({ type: 'gate', id: gate.node.id })}
            style={{
              padding: 0, borderRadius: 10, background: '#0f172a',
              border: `1px solid ${isFocused ? sc : '#1e293b'}`,
              cursor: 'pointer', overflow: 'hidden',
              transition: 'border-color 0.15s',
            }}
          >
            {/* Status bar */}
            <div style={{ height: 3, background: sc + '30' }}>
              <div style={{
                height: '100%', background: sc,
                width: gate.state === 'active' ? '100%' : gate.state === 'latched' ? '100%' : gate.state === 'armed' ? '70%' : '20%',
                borderRadius: '0 2px 2px 0',
              }} />
            </div>

            <div style={{ padding: 8 }}>
              {/* Header */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span>{stateEmoji[gate.state]}</span>
                  <span style={{ fontSize: 11, fontWeight: 700, fontFamily: 'monospace' }}>
                    {humanLabel(gate.node.code, analysis?.humanLabels)}
                  </span>
                </div>
                <span style={{
                  fontSize: 8, fontWeight: 700, padding: '2px 6px', borderRadius: 4,
                  background: sc + '20', color: sc, textTransform: 'uppercase',
                }}>
                  {gate.state}
                </span>
              </div>

              {/* Meta row */}
              <div style={{ display: 'flex', gap: 8, fontSize: 9, color: '#64748b', marginBottom: 4 }}>
                {gate.node.region && <span>Region: {gate.node.region}</span>}
                <span>W: {gate.node.weight}{'\u00D7'}</span>
                <span>C: {gate.node.betweenness.toFixed(2)}</span>
                {gate.loopCount > 0 && (
                  <span style={{ color: '#8b5cf6' }}>{gate.loopCount} loop{gate.loopCount > 1 ? 's' : ''}</span>
                )}
              </div>

              {/* Condition formula */}
              <div style={{
                padding: '3px 6px', borderRadius: 4, background: '#1e293b',
                fontFamily: 'monospace', fontSize: 9, color: '#94a3b8', marginBottom: 4,
              }}>
                {gate.conditionFormula}
              </div>

              {/* Inputs */}
              {gate.inputs.length > 0 && (
                <div style={{ marginBottom: 4 }}>
                  <div style={{ fontSize: 8, fontWeight: 700, color: '#475569', textTransform: 'uppercase', marginBottom: 2 }}>
                    Inputs ({gate.inputs.length})
                  </div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
                    {gate.inputs.map((inp, i) => {
                      const color = TYPE_COLORS[inp.node.type] ?? '#64748b';
                      return (
                        <span
                          key={i}
                          onClick={(e) => { e.stopPropagation(); onFocus({ type: 'entity', id: inp.node.id }); }}
                          style={{
                            padding: '1px 5px', borderRadius: 4, fontSize: 8, fontFamily: 'monospace',
                            background: color + '20', color: color, cursor: 'pointer',
                            display: 'flex', alignItems: 'center', gap: 3,
                          }}
                        >
                          {humanLabel(inp.node.code, analysis?.humanLabels)} {stateIcon(inp.node.state)}
                          {inp.gain != null && (
                            <span style={{ color: '#475569', fontSize: 7 }}>g:{inp.gain}</span>
                          )}
                        </span>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Outputs */}
              {gate.outputs.length > 0 && (
                <div>
                  <div style={{ fontSize: 8, fontWeight: 700, color: '#475569', textTransform: 'uppercase', marginBottom: 2 }}>
                    Outputs ({gate.outputs.length})
                  </div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
                    {gate.outputs.map((out, i) => (
                      <span
                        key={i}
                        onClick={(e) => { e.stopPropagation(); onFocus({ type: 'entity', id: out.node.id }); }}
                        style={{
                          padding: '1px 5px', borderRadius: 4, fontSize: 8, fontFamily: 'monospace',
                          background: (TYPE_COLORS[out.node.type] ?? '#64748b') + '20',
                          color: TYPE_COLORS[out.node.type] ?? '#94a3b8', cursor: 'pointer',
                        }}
                      >
                        {out.operator} {'\u2192'} {humanLabel(out.node.code, analysis?.humanLabels)}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
