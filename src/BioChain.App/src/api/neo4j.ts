// Neo4j HTTP API client (via transactional Cypher endpoint)

const NEO4J_URL = 'http://localhost:7474';
const NEO4J_USER = 'neo4j';
const NEO4J_PASS = 'biochain_graph';

export interface Neo4jRecord {
  [key: string]: unknown;
}

export async function cypher<T = Neo4jRecord>(query: string, params: Record<string, unknown> = {}): Promise<T[]> {
  const auth = btoa(`${NEO4J_USER}:${NEO4J_PASS}`);
  const res = await fetch(`${NEO4J_URL}/db/neo4j/tx/commit`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Basic ${auth}`,
    },
    body: JSON.stringify({
      statements: [{ statement: query, parameters: params, resultDataContents: ['row'] }],
    }),
  });
  if (!res.ok) throw new Error(`Neo4j HTTP ${res.status}`);
  const data = await res.json();
  if (data.errors?.length) throw new Error(data.errors[0].message);

  const result = data.results?.[0];
  if (!result) return [];

  const columns: string[] = result.columns;
  return result.data.map((d: { row: unknown[] }) => {
    const obj: Record<string, unknown> = {};
    columns.forEach((col, i) => { obj[col] = d.row[i]; });
    return obj as T;
  });
}

export interface Neo4jSummary {
  programs: { stdb_id: number; name: string; synced_at: string }[];
  nodes: { stdb_id: number; code: string; kind: string; region: string | null; rank_tag: string; state_sym: string | null; labels: string[] }[];
  edges: { stdb_id: number; type: string; source_code: string; target_code: string; coeff: number | null }[];
  tensors: { stdb_id: number; logic: string; effect_action: string | null }[];
  diags: { stdb_id: number; kind: string; name: string | null }[];
  deltaOps: { stdb_id: number; trigger_code: string; target_code: string }[];
  metaOps: { stdb_id: number; rank_tag: string; target_code: string | null }[];
  convs: { stdb_id: number; kind: string; signal_code: string | null }[];
  totalNodes: number;
  totalEdges: number;
}

export async function getNeo4jSummary(): Promise<Neo4jSummary> {
  const [programs, nodes, edges, tensors, diags, deltaOps, metaOps, convs, counts] = await Promise.all([
    cypher(`MATCH (p:Program) RETURN p.stdb_id AS stdb_id, p.name AS name, toString(p.synced_at) AS synced_at ORDER BY p.stdb_id`),
    cypher(`MATCH (n:Node) RETURN n.stdb_id AS stdb_id, n.code AS code, n.kind AS kind, n.region AS region, n.rank_tag AS rank_tag, n.state_sym AS state_sym, labels(n) AS labels ORDER BY n.stdb_id`),
    cypher(`MATCH (src:Node)-[r]->(tgt:Node) RETURN r.stdb_id AS stdb_id, type(r) AS type, src.code AS source_code, tgt.code AS target_code, r.coeff AS coeff ORDER BY r.stdb_id`),
    cypher(`MATCH (t:Tensor) RETURN t.stdb_id AS stdb_id, t.logic AS logic, t.effect_action AS effect_action ORDER BY t.stdb_id`),
    cypher(`MATCH (d:Diag) RETURN d.stdb_id AS stdb_id, d.kind AS kind, d.name AS name ORDER BY d.stdb_id`),
    cypher(`MATCH (d:DeltaOp) RETURN d.stdb_id AS stdb_id, d.trigger_code AS trigger_code, d.target_code AS target_code ORDER BY d.stdb_id`),
    cypher(`MATCH (m:MetaOp) RETURN m.stdb_id AS stdb_id, m.rank_tag AS rank_tag, m.target_code AS target_code ORDER BY m.stdb_id`),
    cypher(`MATCH (c:Conv) RETURN c.stdb_id AS stdb_id, c.kind AS kind, c.signal_code AS signal_code ORDER BY c.stdb_id`),
    cypher(`MATCH (n) RETURN count(n) AS totalNodes, count{ ()-[r]->() } AS totalEdges`),
  ]);

  return {
    programs: programs as Neo4jSummary['programs'],
    nodes: nodes as Neo4jSummary['nodes'],
    edges: edges as Neo4jSummary['edges'],
    tensors: tensors as Neo4jSummary['tensors'],
    diags: diags as Neo4jSummary['diags'],
    deltaOps: deltaOps as Neo4jSummary['deltaOps'],
    metaOps: metaOps as Neo4jSummary['metaOps'],
    convs: convs as Neo4jSummary['convs'],
    totalNodes: (counts[0]?.totalNodes as number) ?? 0,
    totalEdges: (counts[0]?.totalEdges as number) ?? 0,
  };
}
