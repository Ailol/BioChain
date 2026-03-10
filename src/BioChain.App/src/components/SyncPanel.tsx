import { useState } from 'react';
import { sql } from '../api/client';
import { getNeo4jSummary, type Neo4jSummary } from '../api/neo4j';
import type { Node, Edge, Tensor, DeltaOp, MetaOp, Conv } from '../api/types';

interface StdbCounts {
  programs: number;
  nodes: number;
  edges: number;
  tensors: number;
  diags: number;
  deltaOps: number;
  metaOps: number;
  convs: number;
}

interface StdbData {
  counts: StdbCounts;
  nodes: Node[];
  edges: Edge[];
  tensors: Tensor[];
  deltaOps: DeltaOp[];
  metaOps: MetaOp[];
  convs: Conv[];
}

type SyncStatus = 'idle' | 'loading' | 'done' | 'error' | 'syncing';

export function SyncPanel({ programId }: { programId: number }) {
  const [stdb, setStdb] = useState<StdbData | null>(null);
  const [neo4j, setNeo4j] = useState<Neo4jSummary | null>(null);
  const [status, setStatus] = useState<SyncStatus>('idle');
  const [error, setError] = useState('');
  const [syncLog, setSyncLog] = useState('');

  const loadBoth = async () => {
    setStatus('loading');
    setError('');
    try {
      // Query SpacetimeDB
      const [nodes, edges, tensors, diags, deltaOps, metaOps, convs] = await Promise.all([
        sql<Node>(`SELECT * FROM node WHERE program_id = ${programId}`),
        sql<Edge>(`SELECT * FROM edge WHERE program_id = ${programId}`),
        sql<Tensor>(`SELECT * FROM tensor WHERE program_id = ${programId}`),
        sql<{ id: number }>(`SELECT id FROM diag WHERE program_id = ${programId}`),
        sql<DeltaOp>(`SELECT * FROM delta_op WHERE program_id = ${programId}`),
        sql<MetaOp>(`SELECT * FROM meta_op WHERE program_id = ${programId}`),
        sql<Conv>(`SELECT * FROM conv WHERE program_id = ${programId}`),
      ]);

      setStdb({
        counts: {
          programs: 1,
          nodes: nodes.length,
          edges: edges.length,
          tensors: tensors.length,
          diags: diags.length,
          deltaOps: deltaOps.length,
          metaOps: metaOps.length,
          convs: convs.length,
        },
        nodes,
        edges,
        tensors,
        deltaOps,
        metaOps,
        convs,
      });

      // Query Neo4j
      const neo4jData = await getNeo4jSummary();
      setNeo4j(neo4jData);
      setStatus('done');
    } catch (e: unknown) {
      setError(String(e));
      setStatus('error');
    }
  };

  const runSync = async () => {
    setStatus('syncing');
    setSyncLog('');
    try {
      const res = await fetch('/api/sync-neo4j', { method: 'POST' });
      if (!res.ok) {
        // Fallback: call the script directly isn't possible from browser,
        // so we call the sync script via a simple proxy or show instructions
        setSyncLog('Run sync manually:\n  node scripts/sync-spacetime-neo4j.mjs --wipe');
        setStatus('done');
        return;
      }
      const text = await res.text();
      setSyncLog(text);
      // Reload data after sync
      await loadBoth();
    } catch {
      setSyncLog('Run sync manually:\n  node scripts/sync-spacetime-neo4j.mjs --wipe');
      setStatus('done');
    }
  };

  const match = (stdbCount: number, neo4jCount: number) => {
    if (stdbCount === neo4jCount) return 'match';
    if (neo4jCount === 0 && stdbCount > 0) return 'missing';
    if (neo4jCount < stdbCount) return 'partial';
    if (neo4jCount > stdbCount) return 'extra';
    return 'match';
  };

  return (
    <div className="panel">
      <h2>Sync Check — Program #{programId}</h2>
      <div className="form-row">
        <button onClick={loadBoth} disabled={status === 'loading'}>
          {status === 'loading' ? 'Loading...' : 'Compare'}
        </button>
        <button onClick={runSync} disabled={status === 'syncing'}>
          {status === 'syncing' ? 'Syncing...' : 'Sync Now'}
        </button>
      </div>

      {error && <pre className="log err">{error}</pre>}

      {stdb && neo4j && (
        <>
          {/* Count comparison table */}
          <h3>Entity Counts</h3>
          <table className="sync-table">
            <thead>
              <tr><th>Entity</th><th>SpacetimeDB</th><th>Neo4j</th><th>Status</th></tr>
            </thead>
            <tbody>
              {([
                ['Programs', stdb.counts.programs, neo4j.programs.length],
                ['Nodes', stdb.counts.nodes, neo4j.nodes.length],
                ['Edges', stdb.counts.edges, neo4j.edges.length],
                ['Tensors', stdb.counts.tensors, neo4j.tensors.length],
                ['Diags', stdb.counts.diags, neo4j.diags.length],
                ['DeltaOps', stdb.counts.deltaOps, neo4j.deltaOps.length],
                ['MetaOps', stdb.counts.metaOps, neo4j.metaOps.length],
                ['Convergence', stdb.counts.convs, neo4j.convs.length],
              ] as [string, number, number][]).map(([name, s, n]) => {
                const st = match(s, n);
                return (
                  <tr key={name}>
                    <td>{name}</td>
                    <td>{s}</td>
                    <td>{n}</td>
                    <td><span className={`sync-badge ${st}`}>{st}</span></td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {/* Node detail comparison */}
          {stdb.nodes.length > 0 && (
            <>
              <h3>Nodes — SpacetimeDB</h3>
              <table>
                <thead>
                  <tr><th>ID</th><th>Code</th><th>Kind</th><th>Region</th><th>Rank</th><th>State</th><th>Root</th><th>Neo4j</th></tr>
                </thead>
                <tbody>
                  {stdb.nodes.map(n => {
                    const inNeo4j = neo4j.nodes.find(nn => nn.stdb_id === n.id);
                    const stateMatch = inNeo4j
                      ? (inNeo4j.state_sym ?? null) === (n.state?.sym ?? null)
                      : false;
                    return (
                      <tr key={n.id}>
                        <td>{n.id}</td>
                        <td><strong>{n.code}</strong></td>
                        <td>{n.kind}</td>
                        <td>{n.region ?? '—'}</td>
                        <td>{n.rank_tag}</td>
                        <td>{n.state?.sym ?? '—'}</td>
                        <td>{n.is_root ? '✓' : ''}</td>
                        <td>
                          {inNeo4j ? (
                            <span className={`sync-badge ${stateMatch ? 'match' : 'partial'}`}>
                              {stateMatch ? 'OK' : `sym: ${inNeo4j.state_sym ?? '—'}`}
                            </span>
                          ) : (
                            <span className="sync-badge missing">missing</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}

          {/* Neo4j nodes not in SpacetimeDB */}
          {neo4j.nodes.filter(nn => !stdb.nodes.find(sn => sn.id === nn.stdb_id)).length > 0 && (
            <>
              <h3>Neo4j Extra Nodes (not in SpacetimeDB)</h3>
              <table>
                <thead>
                  <tr><th>STDB ID</th><th>Code</th><th>Kind</th><th>Region</th><th>Labels</th></tr>
                </thead>
                <tbody>
                  {neo4j.nodes
                    .filter(nn => !stdb.nodes.find(sn => sn.id === nn.stdb_id))
                    .map(nn => (
                      <tr key={nn.stdb_id}>
                        <td>{nn.stdb_id}</td>
                        <td>{nn.code}</td>
                        <td>{nn.kind}</td>
                        <td>{nn.region ?? '—'}</td>
                        <td>{nn.labels?.join(', ')}</td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </>
          )}

          {/* Edge comparison */}
          {stdb.edges.length > 0 && (
            <>
              <h3>Edges — SpacetimeDB</h3>
              <table>
                <thead>
                  <tr><th>ID</th><th>Src ID</th><th>Tgt ID</th><th>Type</th><th>Rank</th><th>Coeff</th><th>Gate</th><th>Protocol</th><th>Neo4j</th></tr>
                </thead>
                <tbody>
                  {stdb.edges.map(e => {
                    const inNeo4j = neo4j.edges.find(ne => ne.stdb_id === e.id);
                    return (
                      <tr key={e.id}>
                        <td>{e.id}</td>
                        <td>{e.source_id}</td>
                        <td>{e.target_id}</td>
                        <td>{e.edge_type ?? '→'}</td>
                        <td>{e.rank_tag}</td>
                        <td>{e.coeff}</td>
                        <td>{e.gate ? `${e.gate.code}@${e.gate.region}` : '—'}</td>
                        <td>{e.protocol ? `g=${e.protocol.gain}` : '—'}</td>
                        <td>
                          {inNeo4j ? (
                            <span className="sync-badge match">OK</span>
                          ) : (
                            <span className="sync-badge missing">missing</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}

          {/* Tensors */}
          {stdb.tensors.length > 0 && (
            <>
              <h3>Tensors (R3)</h3>
              <table>
                <thead>
                  <tr><th>ID</th><th>Logic</th><th>Conditions</th><th>Effect</th><th>Neo4j</th></tr>
                </thead>
                <tbody>
                  {stdb.tensors.map(t => {
                    const inNeo4j = neo4j.tensors.find(nt => nt.stdb_id === t.id);
                    return (
                      <tr key={t.id}>
                        <td>{t.id}</td>
                        <td>{t.logic}</td>
                        <td>{t.conditions?.map(c => `${c.negated ? '!' : ''}${c.code}@${c.region}[${c.state}]`).join(', ')}</td>
                        <td>{t.effect ? `${t.effect.action}(${t.effect.code}@${t.effect.region})` : '—'}</td>
                        <td>
                          <span className={`sync-badge ${inNeo4j ? 'match' : 'missing'}`}>{inNeo4j ? 'OK' : 'missing'}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}

          {/* DeltaOps */}
          {stdb.deltaOps.length > 0 && (
            <>
              <h3>DeltaOps (Plasticity)</h3>
              <table>
                <thead>
                  <tr><th>ID</th><th>Rank</th><th>Trigger</th><th>Target</th><th>Change</th><th>Tau</th><th>Neo4j</th></tr>
                </thead>
                <tbody>
                  {stdb.deltaOps.map(d => {
                    const inNeo4j = neo4j.deltaOps.find(nd => nd.stdb_id === d.id);
                    return (
                      <tr key={d.id}>
                        <td>{d.id}</td>
                        <td>{d.rank_tag}</td>
                        <td>{d.trigger_code}@{d.trigger_region}[{d.trigger_state}]</td>
                        <td>{d.target_code}@{d.target_region}</td>
                        <td>{d.change?.property}: {d.change?.before}→{d.change?.after}</td>
                        <td>{d.tau}</td>
                        <td>
                          <span className={`sync-badge ${inNeo4j ? 'match' : 'missing'}`}>{inNeo4j ? 'OK' : 'missing'}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}

          {/* MetaOps */}
          {stdb.metaOps.length > 0 && (
            <>
              <h3>MetaOps</h3>
              <table>
                <thead>
                  <tr><th>ID</th><th>Rank</th><th>Window</th><th>Target</th><th>Neo4j</th></tr>
                </thead>
                <tbody>
                  {stdb.metaOps.map(m => {
                    const inNeo4j = neo4j.metaOps.find(nm => nm.stdb_id === m.id);
                    return (
                      <tr key={m.id}>
                        <td>{m.id}</td>
                        <td>{m.rank_tag}</td>
                        <td>{m.window?.kind}: {m.window?.value}</td>
                        <td>{m.target?.code}@{m.target?.region} .{m.target?.property}</td>
                        <td>
                          <span className={`sync-badge ${inNeo4j ? 'match' : 'missing'}`}>{inNeo4j ? 'OK' : 'missing'}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}

          {/* Convergence */}
          {stdb.convs.length > 0 && (
            <>
              <h3>Convergence</h3>
              <table>
                <thead>
                  <tr><th>ID</th><th>Kind</th><th>Signal</th><th>Diagnosis</th><th>Neo4j</th></tr>
                </thead>
                <tbody>
                  {stdb.convs.map(c => {
                    const inNeo4j = neo4j.convs.find(nc => nc.stdb_id === c.id);
                    return (
                      <tr key={c.id}>
                        <td>{c.id}</td>
                        <td>{c.kind}</td>
                        <td>{c.signal_code ? `${c.signal_code}@${c.signal_region}` : '—'}</td>
                        <td>{c.diagnosis ?? '—'}</td>
                        <td>
                          <span className={`sync-badge ${inNeo4j ? 'match' : 'missing'}`}>{inNeo4j ? 'OK' : 'missing'}</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </>
          )}

          {/* Neo4j graph overview */}
          <h3>Neo4j Graph Overview</h3>
          <div className="neo4j-stats">
            <span>Total nodes: {neo4j.totalNodes}</span>
            <span>Total relationships: {neo4j.totalEdges}</span>
            <span>Last synced: {neo4j.programs[0]?.synced_at ?? 'never'}</span>
          </div>
        </>
      )}

      {syncLog && <pre className="log">{syncLog}</pre>}
    </div>
  );
}
