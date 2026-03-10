import { useState, useRef, useEffect, useCallback } from 'react';
import cytoscape, { type Core, type ElementDefinition } from 'cytoscape';
import { sql } from '../api/client';
import { addNode, addEdge, addTensor } from '../api/reducers';
import { parseBnf } from '../api/bnfParser';
import type { Node, Edge, Tensor, DeltaOp, Program } from '../api/types';

// ── colour palette by kind ──
const KIND_COLORS: Record<string, string> = {
  'L.nt': '#6c8cff',  // neurotransmitter
  'L.h':  '#ff8c42',  // hormone
  'L.p':  '#4caf50',  // peptide
  'R':    '#ab47bc',  // receptor
  'K':    '#f44336',  // kinase/enzyme
  'Gp':   '#ffca28',  // G-protein
  '2m':   '#26c6da',  // second messenger
  'N':    '#e0e0e8',  // neuron
  'G':    '#78909c',  // glia
  'T':    '#8d6e63',  // transporter
  'E':    '#ef5350',  // enzyme
};

function nodeColor(kind: string): string {
  if (KIND_COLORS[kind]) return KIND_COLORS[kind];
  // check prefix
  const prefix = kind.split('.')[0].split(':')[0];
  return KIND_COLORS[prefix] ?? '#888898';
}

// ── edge styling by operator ──
const EDGE_STYLES: Record<string, { color: string; style: string; target: string }> = {
  '→':  { color: '#6c8cff', style: 'solid',  target: 'triangle' },
  '⊣':  { color: '#f44336', style: 'solid',  target: 'tee' },
  '⇌':  { color: '#ffca28', style: 'solid',  target: 'triangle' },
  '↺':  { color: '#26c6da', style: 'dashed', target: 'triangle' },
  '⊸':  { color: '#ab47bc', style: 'dotted', target: 'diamond' },
  'R2':  { color: '#4caf50', style: 'dashed', target: 'vee' },
};

function edgeStyle(type: string | null) {
  return EDGE_STYLES[type ?? '→'] ?? EDGE_STYLES['→'];
}

type Layout = 'cose' | 'circle' | 'grid' | 'breadthfirst';

export function GraphPanel({ programId }: { programId: number }) {
  const cyRef = useRef<Core | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [nodes, setNodes] = useState<Node[]>([]);
  const [edges, setEdges] = useState<Edge[]>([]);
  const [tensors, setTensors] = useState<Tensor[]>([]);
  const [deltaOps, setDeltaOps] = useState<DeltaOp[]>([]);
  const [log, setLog] = useState('');
  const [parsing, setParsing] = useState(false);
  const [layout, setLayout] = useState<Layout>('cose');
  const [selected, setSelected] = useState<string>('');
  const [filterRank, setFilterRank] = useState<string>('all');

  // Node form
  const [nCode, setNCode] = useState('');
  const [nType, setNType] = useState('L.nt');
  const [nRegion, setNRegion] = useState('');
  const [nRank, setNRank] = useState('R0');
  const [nSym, setNSym] = useState('');
  const [nIsRoot, setNIsRoot] = useState(false);

  // Edge form
  const [eSrcId, setESrcId] = useState('');
  const [eTgtId, setETgtId] = useState('');
  const [eType, setEType] = useState('→');
  const [eCoeff, setECoeff] = useState('1.0');

  const loadGraph = useCallback(async () => {
    try {
      const [n, e, t, d] = await Promise.all([
        sql<Node>(`SELECT * FROM node WHERE program_id = ${programId}`),
        sql<Edge>(`SELECT * FROM edge WHERE program_id = ${programId}`),
        sql<Tensor>(`SELECT * FROM tensor WHERE program_id = ${programId}`),
        sql<DeltaOp>(`SELECT * FROM delta_op WHERE program_id = ${programId}`),
      ]);

      // Auto-parse: if raw_base exists but DB has few nodes, parse and populate
      if (n.length <= 1) {
        const programs = await sql<Program>(`SELECT * FROM program WHERE id = ${programId}`);
        const program = programs[0];
        if (program?.raw_base) {
          setParsing(true);
          setLog('Auto-parsing BNF...');
          const result = parseBnf(program.raw_base);

          const nodeKeyToId = new Map<string, number>();
          for (const existing of n) {
            const key = existing.region ? `${existing.kind}:${existing.code}@${existing.region}` : `${existing.kind}:${existing.code}`;
            nodeKeyToId.set(key, existing.id);
          }

          let addedNodes = 0, addedEdges = 0, addedTensors = 0;

          for (const pn of result.nodes) {
            const key = pn.region ? `${pn.kind}:${pn.code}@${pn.region}` : `${pn.kind}:${pn.code}`;
            if (nodeKeyToId.has(key)) continue;
            const state = pn.sym ? { sym: pn.sym, delta_sign: pn.deltaSign, delta_val: pn.deltaVal } : null;
            await addNode(programId, pn.code, pn.kind, pn.region, pn.rank, state, pn.isRoot, pn.fieldOps, pn.props);
            addedNodes++;
          }

          // Reload to get assigned IDs
          const allNodes = await sql<Node>(`SELECT * FROM node WHERE program_id = ${programId}`);
          for (const nd of allNodes) {
            const key = nd.region ? `${nd.kind}:${nd.code}@${nd.region}` : `${nd.kind}:${nd.code}`;
            nodeKeyToId.set(key, nd.id);
          }

          for (const pe of result.edges) {
            const srcId = nodeKeyToId.get(pe.sourceKey);
            const tgtId = nodeKeyToId.get(pe.targetKey);
            if (srcId == null || tgtId == null) continue;
            await addEdge(programId, srcId, tgtId, pe.rank, pe.edgeType, pe.coeff,
              pe.gate, pe.protoLabel, pe.chain, pe.chainPos, pe.ringId);
            addedEdges++;
          }

          for (const pt of result.tensors) {
            await addTensor(programId, pt.conditions, pt.logic, pt.effect, pt.label);
            addedTensors++;
          }

          setParsing(false);

          // Re-fetch everything after population
          const [n2, e2, t2, d2] = await Promise.all([
            sql<Node>(`SELECT * FROM node WHERE program_id = ${programId}`),
            sql<Edge>(`SELECT * FROM edge WHERE program_id = ${programId}`),
            sql<Tensor>(`SELECT * FROM tensor WHERE program_id = ${programId}`),
            sql<DeltaOp>(`SELECT * FROM delta_op WHERE program_id = ${programId}`),
          ]);
          setNodes(n2); setEdges(e2); setTensors(t2); setDeltaOps(d2);
          setLog(`Auto-parsed: +${addedNodes} nodes, +${addedEdges} edges, +${addedTensors} tensors`);
          return;
        }
      }

      setNodes(n); setEdges(e); setTensors(t); setDeltaOps(d);
      setLog(`Loaded ${n.length} nodes, ${e.length} edges, ${t.length} tensors, ${d.length} Δ-ops`);
    } catch (e: unknown) {
      setLog(String(e));
      setParsing(false);
    }
  }, [programId]);

  // Auto-load graph on mount / programId change
  useEffect(() => { loadGraph(); }, [loadGraph]);

  const createNode = async () => {
    try {
      const state = nSym ? { sym: nSym, delta_sign: null, delta_val: null } : null;
      await addNode(programId, nCode, nType, nRegion || null, nRank, state, nIsRoot);
      setLog(`Added node ${nCode}`);
      setNCode('');
      setNSym('');
      await loadGraph();
    } catch (e: unknown) {
      setLog(String(e));
    }
  };

  const createEdge = async () => {
    try {
      await addEdge(programId, Number(eSrcId), Number(eTgtId), 'R0', eType, Number(eCoeff));
      setLog(`Added edge ${eSrcId}→${eTgtId}`);
      await loadGraph();
    } catch (e: unknown) {
      setLog(String(e));
    }
  };

  // ── build cytoscape elements ──
  const buildElements = useCallback((): ElementDefinition[] => {
    const els: ElementDefinition[] = [];

    const filteredNodes = filterRank === 'all'
      ? nodes
      : nodes.filter(n => n.rank_tag === filterRank);
    const nodeIds = new Set(filteredNodes.map(n => n.id));

    for (const n of filteredNodes) {
      const label = n.region ? `${n.code}@${n.region}` : n.code;
      const stateLabel = n.state?.sym ? ` [${n.state.sym}]` : '';
      els.push({
        data: {
          id: `n${n.id}`,
          label: label + stateLabel,
          color: nodeColor(n.kind),
          kind: n.kind,
          rank: n.rank_tag,
          isRoot: n.is_root,
          size: n.is_root ? 40 : 28,
          nodeData: n,
        },
      });
    }

    for (const e of edges) {
      if (!nodeIds.has(e.source_id) || !nodeIds.has(e.target_id)) continue;
      const es = edgeStyle(e.edge_type);
      els.push({
        data: {
          id: `e${e.id}`,
          source: `n${e.source_id}`,
          target: `n${e.target_id}`,
          label: e.edge_type ?? '→',
          color: es.color,
          lineStyle: es.style,
          targetArrow: es.target,
          coeff: e.coeff,
          edgeData: e,
        },
      });
    }

    // tensor nodes (conditional logic)
    for (const t of tensors) {
      const condNodes = t.conditions?.map(c => `n${nodes.find(n => n.code === c.code)?.id}`).filter(Boolean) ?? [];
      els.push({
        data: {
          id: `t${t.id}`,
          label: `⊗ ${t.logic}`,
          color: '#ff9800',
          kind: 'tensor',
          size: 22,
        },
      });
      // connect condition nodes to tensor
      for (const src of condNodes) {
        els.push({
          data: {
            id: `tc${t.id}-${src}`,
            source: src,
            target: `t${t.id}`,
            label: '?',
            color: '#ff980066',
            lineStyle: 'dotted',
            targetArrow: 'diamond',
          },
        });
      }
      // connect tensor to effect node
      if (t.effect) {
        const effectNode = nodes.find(n => n.code === t.effect.code);
        if (effectNode && nodeIds.has(effectNode.id)) {
          els.push({
            data: {
              id: `te${t.id}`,
              source: `t${t.id}`,
              target: `n${effectNode.id}`,
              label: t.effect.action,
              color: '#ff9800',
              lineStyle: 'dashed',
              targetArrow: 'triangle',
            },
          });
        }
      }
    }

    return els;
  }, [nodes, edges, tensors, filterRank]);

  // ── render / update cytoscape ──
  useEffect(() => {
    if (!containerRef.current || nodes.length === 0) return;

    const elements = buildElements();

    if (cyRef.current) {
      cyRef.current.destroy();
    }

    const cy = cytoscape({
      container: containerRef.current,
      elements,
      style: [
        {
          selector: 'node',
          style: {
            'background-color': 'data(color)',
            'label': 'data(label)',
            'color': '#e0e0e8',
            'font-size': '11px',
            'font-family': "'JetBrains Mono', monospace",
            'text-valign': 'bottom',
            'text-margin-y': 6,
            'width': 'data(size)',
            'height': 'data(size)',
            'border-width': 2,
            'border-color': '#2a2a3a',
            'text-outline-width': 2,
            'text-outline-color': '#0a0a0f',
          },
        },
        {
          selector: 'node[?isRoot]',
          style: {
            'border-width': 3,
            'border-color': '#6c8cff',
            'shape': 'diamond',
          },
        },
        {
          selector: 'node[kind = "tensor"]',
          style: {
            'shape': 'hexagon',
            'border-color': '#ff9800',
          },
        },
        {
          selector: 'node:selected',
          style: {
            'border-color': '#fff',
            'border-width': 3,
          },
        },
        {
          selector: 'edge',
          style: {
            'line-color': 'data(color)',
            'target-arrow-color': 'data(color)',
            'target-arrow-shape': 'data(targetArrow)',
            'curve-style': 'bezier',
            'width': 2,
            'arrow-scale': 1.2,
            'label': 'data(label)',
            'font-size': '9px',
            'color': '#888898',
            'text-rotation': 'autorotate',
            'text-outline-width': 2,
            'text-outline-color': '#0a0a0f',
            'line-style': 'data(lineStyle)' as unknown as string,
          },
        },
        {
          selector: 'edge:selected',
          style: {
            'width': 4,
            'line-color': '#fff',
            'target-arrow-color': '#fff',
          },
        },
      ],
      layout: {
        name: layout,
        animate: true,
        animationDuration: 500,
        ...(layout === 'cose' ? {
          idealEdgeLength: 120,
          nodeRepulsion: 8000,
          gravity: 0.5,
          padding: 30,
        } : {}),
        ...(layout === 'breadthfirst' ? {
          directed: true,
          padding: 20,
          spacingFactor: 1.5,
        } : {}),
      } as cytoscape.LayoutOptions,
    });

    // click handler
    cy.on('tap', 'node', (evt) => {
      const data = evt.target.data();
      if (data.nodeData) {
        const n = data.nodeData as Node;
        setSelected(
          `NODE #${n.id}: ${n.code} (${n.kind})` +
          `\nRegion: ${n.region ?? '—'}  Rank: ${n.rank_tag}  Root: ${n.is_root}` +
          `\nState: ${n.state ? `${n.state.sym} Δ${n.state.delta_sign ?? ''}${n.state.delta_val ?? ''}` : '—'}` +
          (n.integ ? `\nIntegration: ${n.integ.inputs?.length ?? 0} inputs → ${n.integ.output?.code ?? '?'}` : '')
        );
      } else {
        setSelected(`TENSOR #${data.id}: ${data.label}`);
      }
    });

    cy.on('tap', 'edge', (evt) => {
      const data = evt.target.data();
      if (data.edgeData) {
        const e = data.edgeData as Edge;
        setSelected(
          `EDGE #${e.id}: ${e.source_id} ${e.edge_type ?? '→'} ${e.target_id}` +
          `\nCoeff: ${e.coeff}  Rank: ${e.rank_tag}` +
          (e.gate ? `\nGate: ${e.gate.code}@${e.gate.region} (${e.gate.mode})` : '') +
          (e.protocol ? `\nProtocol: gain=${e.protocol.gain}, τ=${e.protocol.tau_class}` : '')
        );
      }
    });

    cy.on('tap', (evt) => {
      if (evt.target === cy) setSelected('');
    });

    cyRef.current = cy;

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, [nodes, edges, tensors, layout, filterRank, buildElements]);

  // re-layout
  const runLayout = (name: Layout) => {
    setLayout(name);
  };

  return (
    <div className="panel">
      <h2>Graph — Program #{programId}</h2>

      {/* toolbar */}
      <div className="form-row">
        <button onClick={loadGraph} disabled={parsing}>
          {parsing ? 'Parsing...' : 'Refresh'}
        </button>
        <span style={{ color: 'var(--text-dim)', fontSize: '0.8rem' }}>Layout:</span>
        {(['cose', 'circle', 'grid', 'breadthfirst'] as Layout[]).map(l => (
          <button key={l} className={layout === l ? 'active' : ''} onClick={() => runLayout(l)}>
            {l === 'cose' ? 'Force' : l === 'breadthfirst' ? 'Tree' : l.charAt(0).toUpperCase() + l.slice(1)}
          </button>
        ))}
        <span style={{ color: 'var(--text-dim)', fontSize: '0.8rem', marginLeft: '0.5rem' }}>Rank:</span>
        <select value={filterRank} onChange={e => setFilterRank(e.target.value)}>
          <option value="all">All</option>
          <option value="R0">R0</option>
          <option value="R1">R1</option>
        </select>
      </div>

      {/* legend */}
      <div className="graph-legend">
        {Object.entries(KIND_COLORS).slice(0, 8).map(([kind, color]) => (
          <span key={kind} className="legend-item">
            <span className="legend-dot" style={{ background: color }} />
            {kind}
          </span>
        ))}
      </div>

      {/* cytoscape container */}
      <div ref={containerRef} className="graph-canvas" />

      {/* detail panel */}
      {selected && <pre className="log">{selected}</pre>}

      {/* add node / edge forms (collapsed) */}
      <details>
        <summary style={{ color: 'var(--text-dim)', cursor: 'pointer', margin: '0.5rem 0' }}>Add Node / Edge</summary>
        <h3>Add Node</h3>
        <div className="form-row">
          <input placeholder="Code (e.g. DA)" value={nCode} onChange={e => setNCode(e.target.value)} />
          <input placeholder="Kind (e.g. L.nt)" value={nType} onChange={e => setNType(e.target.value)} />
          <input placeholder="Region" value={nRegion} onChange={e => setNRegion(e.target.value)} />
          <select value={nRank} onChange={e => setNRank(e.target.value)}>
            <option value="R0">R0</option>
            <option value="R1">R1</option>
          </select>
          <input placeholder="State sym" value={nSym} onChange={e => setNSym(e.target.value)} />
          <label>
            <input type="checkbox" checked={nIsRoot} onChange={e => setNIsRoot(e.target.checked)} />
            Root
          </label>
          <button onClick={createNode}>Add</button>
        </div>

        <h3>Add Edge</h3>
        <div className="form-row">
          <input placeholder="Source ID" value={eSrcId} onChange={e => setESrcId(e.target.value)} />
          <input placeholder="Target ID" value={eTgtId} onChange={e => setETgtId(e.target.value)} />
          <select value={eType} onChange={e => setEType(e.target.value)}>
            <option value="→">→ excite</option>
            <option value="⊣">⊣ inhibit</option>
            <option value="⇌">⇌ bidir</option>
            <option value="↺">↺ feedback</option>
            <option value="⊸">⊸ gate</option>
          </select>
          <input placeholder="Coeff" value={eCoeff} onChange={e => setECoeff(e.target.value)} style={{ width: 60 }} />
          <button onClick={createEdge}>Add</button>
        </div>
      </details>

      {/* stats */}
      {nodes.length > 0 && (
        <div className="neo4j-stats" style={{ marginTop: '0.5rem' }}>
          <span>Nodes: {nodes.length}</span>
          <span>Edges: {edges.length}</span>
          <span>Tensors: {tensors.length}</span>
          <span>Δ-ops: {deltaOps.length}</span>
        </div>
      )}

      {log && <pre className="log">{log}</pre>}
    </div>
  );
}
