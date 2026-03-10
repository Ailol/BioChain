#!/usr/bin/env node
/**
 * SpacetimeDB → Neo4j sync script
 * Reads all programs, nodes, edges, tensors, diags, delta_ops, meta_ops, conv
 * from SpacetimeDB and creates a graph in Neo4j.
 *
 * Usage: node scripts/sync-spacetime-neo4j.mjs [--wipe]
 *   --wipe  Clear Neo4j before syncing (default: false)
 */

const STDB_URL = process.env.STDB_URL ?? "http://localhost:3000";
const STDB_DB = process.env.STDB_DB ?? "biochain";
const NEO4J_URL = process.env.NEO4J_URL ?? "http://localhost:7474";
const NEO4J_USER = process.env.NEO4J_USER ?? "neo4j";
const NEO4J_PASS = process.env.NEO4J_PASS ?? "biochain_graph";
const WIPE = process.argv.includes("--wipe");

// ── SpacetimeDB SQL query ──────────────────────────────────────────────────

async function stdbSql(query) {
  const res = await fetch(`${STDB_URL}/v1/database/${STDB_DB}/sql`, {
    method: "POST",
    headers: { "Content-Type": "text/plain" },
    body: query,
  });
  if (!res.ok) throw new Error(`STDB SQL failed: ${res.status} ${await res.text()}`);
  const data = await res.json();
  if (!data.length || !data[0].rows) return [];

  const { schema, rows } = data[0];
  const names = schema.elements.map((e) =>
    typeof e.name === "object" ? e.name.some : e.name
  );

  return rows.map((row) => {
    const obj = {};
    names.forEach((n, i) => {
      obj[n] = decodeValue(row[i]);
    });
    return obj;
  });
}

/** Decode SpacetimeDB Option<T> / enum encoding */
function decodeValue(v) {
  if (v === null || v === undefined) return null;
  if (Array.isArray(v)) {
    // Option encoding: [0, value] = Some, [1, []] = None
    if (v.length === 2 && (v[0] === 0 || v[0] === 1)) {
      return v[0] === 0 ? decodeValue(v[1]) : null;
    }
    return v.map(decodeValue);
  }
  if (typeof v === "object" && v !== null) {
    // Timestamp: { __timestamp_micros_since_unix_epoch__: n }
    if ("__timestamp_micros_since_unix_epoch__" in v) {
      return new Date(Number(v.__timestamp_micros_since_unix_epoch__) / 1000).toISOString();
    }
    const decoded = {};
    for (const [k, val] of Object.entries(v)) {
      decoded[k] = decodeValue(val);
    }
    return decoded;
  }
  return v;
}

// ── Neo4j Cypher via HTTP API ──────────────────────────────────────────────

async function cypher(query, params = {}) {
  const auth = Buffer.from(`${NEO4J_USER}:${NEO4J_PASS}`).toString("base64");
  const res = await fetch(`${NEO4J_URL}/db/neo4j/tx/commit`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Basic ${auth}`,
    },
    body: JSON.stringify({
      statements: [{ statement: query, parameters: params }],
    }),
  });
  if (!res.ok) throw new Error(`Neo4j failed: ${res.status} ${await res.text()}`);
  const data = await res.json();
  if (data.errors?.length) {
    throw new Error(`Neo4j error: ${JSON.stringify(data.errors)}`);
  }
  return data.results?.[0] ?? null;
}

// ── Sync logic ─────────────────────────────────────────────────────────────

async function sync() {
  console.log("SpacetimeDB → Neo4j sync");
  console.log(`  STDB: ${STDB_URL}/database/${STDB_DB}`);
  console.log(`  Neo4j: ${NEO4J_URL}`);

  // Wipe if requested
  if (WIPE) {
    console.log("\n  Wiping Neo4j...");
    await cypher("MATCH (n) DETACH DELETE n");
    console.log("  Done.");
  }

  // Create constraints/indexes
  console.log("\n  Creating constraints...");
  await cypher("CREATE CONSTRAINT IF NOT EXISTS FOR (p:Program) REQUIRE p.stdb_id IS UNIQUE");
  await cypher("CREATE CONSTRAINT IF NOT EXISTS FOR (n:Node) REQUIRE n.stdb_id IS UNIQUE");
  await cypher("CREATE INDEX IF NOT EXISTS FOR (n:Node) ON (n.program_id)");
  await cypher("CREATE INDEX IF NOT EXISTS FOR (n:Node) ON (n.code)");

  // ── Programs ──
  const programs = await stdbSql("SELECT * FROM program");
  console.log(`\n  Programs: ${programs.length}`);
  for (const p of programs) {
    await cypher(
      `MERGE (prog:Program {stdb_id: $id})
       SET prog.name = $name,
           prog.phase = $phase,
           prog.domains = $domains,
           prog.tick = $tick,
           prog.created_at = $created_at,
           prog.synced_at = datetime()`,
      {
        id: Number(p.id),
        name: p.name,
        phase: p.phase,
        domains: Array.isArray(p.domains) ? p.domains : [],
        tick: Number(p.tick),
        created_at: p.created_at ?? "",
      }
    );
    console.log(`    ✓ Program ${p.id}: ${p.name}`);
  }

  // ── Nodes ──
  const nodes = await stdbSql("SELECT * FROM node");
  console.log(`\n  Nodes: ${nodes.length}`);
  for (const n of nodes) {
    const state = n.state;
    const labels = ["Node"];

    // Add rank-specific label
    if (n.rank_tag) labels.push(n.rank_tag);

    // Add kind-based label
    const kindLabel = kindToLabel(n.kind);
    if (kindLabel) labels.push(kindLabel);

    await cypher(
      `MERGE (n:Node {stdb_id: $id})
       SET n.program_id = $program_id,
           n.code = $code,
           n.kind = $kind,
           n.region = $region,
           n.rank_tag = $rank_tag,
           n.state_sym = $state_sym,
           n.state_val = $state_val,
           n.is_root = $is_root,
           n.synced_at = datetime()
       WITH n
       MATCH (prog:Program {stdb_id: $program_id})
       MERGE (n)-[:BELONGS_TO]->(prog)`,
      {
        id: Number(n.id),
        program_id: Number(n.program_id),
        code: n.code,
        kind: n.kind ?? "",
        region: n.region ?? null,
        rank_tag: n.rank_tag ?? "",
        state_sym: state?.sym ?? null,
        state_val: state?.val ?? null,
        is_root: n.is_root ?? false,
      }
    );

    // Add labels dynamically (rank + kind)
    for (const label of labels) {
      if (label !== "Node") {
        await cypher(`MATCH (n:Node {stdb_id: $id}) SET n:\`${label}\``, {
          id: Number(n.id),
        });
      }
    }

    console.log(
      `    ✓ Node ${n.id}: ${n.code}${n.region ? "@" + n.region : ""} [${n.rank_tag}] ${state?.sym ?? ""}`
    );
  }

  // ── Edges ──
  const edges = await stdbSql("SELECT * FROM edge");
  console.log(`\n  Edges: ${edges.length}`);
  for (const e of edges) {
    const relType = edgeTypeToRel(e.edge_type, e.rank_tag);
    await cypher(
      `MATCH (src:Node {stdb_id: $source_id})
       MATCH (tgt:Node {stdb_id: $target_id})
       MERGE (src)-[r:\`${relType}\` {stdb_id: $id}]->(tgt)
       SET r.rank_tag = $rank_tag,
           r.coeff = $coeff,
           r.chain = $chain,
           r.chain_pos = $chain_pos,
           r.ring_id = $ring_id,
           r.synced_at = datetime()`,
      {
        id: Number(e.id),
        source_id: Number(e.source_id),
        target_id: Number(e.target_id),
        rank_tag: e.rank_tag ?? "",
        coeff: e.coeff ?? 0,
        chain: e.chain ?? null,
        chain_pos: e.chain_pos ?? null,
        ring_id: e.ring_id ?? null,
      }
    );

    // Add gate properties if present
    if (e.gate) {
      await cypher(
        `MATCH ()-[r:\`${relType}\` {stdb_id: $id}]->()
         SET r.gate_node = $gate_node,
             r.gate_region = $gate_region,
             r.gate_threshold = $gate_threshold`,
        {
          id: Number(e.id),
          gate_node: e.gate.node_code ?? null,
          gate_region: e.gate.region ?? null,
          gate_threshold: e.gate.threshold ?? null,
        }
      );
    }

    // Add protocol properties if present
    if (e.protocol) {
      await cypher(
        `MATCH ()-[r:\`${relType}\` {stdb_id: $id}]->()
         SET r.proto_gain = $gain,
             r.proto_polarity = $polarity,
             r.proto_tau_class = $tau_class,
             r.proto_coupling = $coupling,
             r.proto_release_pr = $release_pr,
             r.proto_label = $label`,
        {
          id: Number(e.id),
          gain: e.protocol.gain ?? null,
          polarity: e.protocol.polarity ?? null,
          tau_class: e.protocol.tau_class ?? null,
          coupling: e.protocol.coupling ?? null,
          release_pr: e.protocol.release_pr ?? null,
          label: e.proto_label ?? null,
        }
      );
    }

    console.log(`    ✓ Edge ${e.id}: Node(${e.source_id}) -[${relType}]-> Node(${e.target_id})`);
  }

  // ── Tensors (R3) ──
  const tensors = await stdbSql("SELECT * FROM tensor");
  console.log(`\n  Tensors: ${tensors.length}`);
  for (const t of tensors) {
    await cypher(
      `MERGE (ten:Tensor {stdb_id: $id})
       SET ten:R3,
           ten.program_id = $program_id,
           ten.logic = $logic,
           ten.label = $label,
           ten.effect_code = $effect_code,
           ten.effect_region = $effect_region,
           ten.effect_action = $effect_action,
           ten.effect_value = $effect_value,
           ten.synced_at = datetime()
       WITH ten
       MATCH (prog:Program {stdb_id: $program_id})
       MERGE (ten)-[:BELONGS_TO]->(prog)`,
      {
        id: Number(t.id),
        program_id: Number(t.program_id),
        logic: t.logic ?? "AND",
        label: t.label ?? null,
        effect_code: t.effect?.code ?? null,
        effect_region: t.effect?.region ?? null,
        effect_action: t.effect?.action ?? null,
        effect_value: t.effect?.value ?? null,
      }
    );

    // Link tensor conditions to nodes
    if (t.conditions) {
      for (const cond of t.conditions) {
        await cypher(
          `MATCH (ten:Tensor {stdb_id: $tensor_id})
           MATCH (n:Node {program_id: $program_id, code: $code})
           WHERE n.region = $region OR ($region IS NULL AND n.region IS NULL)
           MERGE (ten)-[r:CONDITION]->(n)
           SET r.state = $state, r.negated = $negated`,
          {
            tensor_id: Number(t.id),
            program_id: Number(t.program_id),
            code: cond.code,
            region: cond.region ?? null,
            state: cond.state ?? null,
            negated: cond.negated ?? false,
          }
        );
      }
    }
    console.log(`    ✓ Tensor ${t.id}: ${t.logic} → ${t.effect?.action ?? "?"}`);
  }

  // ── Diags ──
  const diags = await stdbSql("SELECT * FROM diag");
  console.log(`\n  Diags: ${diags.length}`);
  for (const d of diags) {
    await cypher(
      `MERGE (diag:Diag {stdb_id: $id})
       SET diag.program_id = $program_id,
           diag.kind = $kind,
           diag.name = $name,
           diag.expr = $expr,
           diag.synced_at = datetime()
       WITH diag
       MATCH (prog:Program {stdb_id: $program_id})
       MERGE (diag)-[:BELONGS_TO]->(prog)`,
      {
        id: Number(d.id),
        program_id: Number(d.program_id),
        kind: d.kind ?? "",
        name: d.name ?? null,
        expr: d.expr ?? "",
      }
    );
    console.log(`    ✓ Diag ${d.id}: ${d.kind} ${d.name ?? ""}`);
  }

  // ── DeltaOps (Plasticity) ──
  const deltas = await stdbSql("SELECT * FROM delta_op");
  console.log(`\n  DeltaOps: ${deltas.length}`);
  for (const d of deltas) {
    await cypher(
      `MERGE (dop:DeltaOp {stdb_id: $id})
       SET dop.program_id = $program_id,
           dop.rank_tag = $rank_tag,
           dop.trigger_code = $trigger_code,
           dop.trigger_region = $trigger_region,
           dop.trigger_state = $trigger_state,
           dop.target_code = $target_code,
           dop.target_region = $target_region,
           dop.change_property = $change_property,
           dop.change_before = $change_before,
           dop.change_after = $change_after,
           dop.tau = $tau,
           dop.synced_at = datetime()
       WITH dop
       MATCH (prog:Program {stdb_id: $program_id})
       MERGE (dop)-[:BELONGS_TO]->(prog)`,
      {
        id: Number(d.id),
        program_id: Number(d.program_id),
        rank_tag: d.rank_tag ?? "",
        trigger_code: d.trigger_code ?? "",
        trigger_region: d.trigger_region ?? "",
        trigger_state: d.trigger_state ?? "",
        target_code: d.target_code ?? "",
        target_region: d.target_region ?? "",
        change_property: d.change?.property ?? null,
        change_before: d.change?.before ?? null,
        change_after: d.change?.after ?? null,
        tau: d.tau ?? "",
      }
    );

    // Link trigger → node
    await cypher(
      `MATCH (dop:DeltaOp {stdb_id: $id})
       MATCH (n:Node {program_id: $program_id, code: $code})
       WHERE n.region = $region OR ($region IS NULL AND n.region IS NULL)
       MERGE (dop)-[:TRIGGERED_BY]->(n)`,
      { id: Number(d.id), program_id: Number(d.program_id), code: d.trigger_code, region: d.trigger_region || null }
    );

    // Link target → node
    await cypher(
      `MATCH (dop:DeltaOp {stdb_id: $id})
       MATCH (n:Node {program_id: $program_id, code: $code})
       WHERE n.region = $region OR ($region IS NULL AND n.region IS NULL)
       MERGE (dop)-[:TARGETS]->(n)`,
      { id: Number(d.id), program_id: Number(d.program_id), code: d.target_code, region: d.target_region || null }
    );

    console.log(`    ✓ DeltaOp ${d.id}: ${d.trigger_code}@${d.trigger_region} → ${d.target_code}@${d.target_region}`);
  }

  // ── MetaOps ──
  const metas = await stdbSql("SELECT * FROM meta_op");
  console.log(`\n  MetaOps: ${metas.length}`);
  for (const m of metas) {
    await cypher(
      `MERGE (mop:MetaOp {stdb_id: $id})
       SET mop.program_id = $program_id,
           mop.rank_tag = $rank_tag,
           mop.window_kind = $window_kind,
           mop.window_value = $window_value,
           mop.target_code = $target_code,
           mop.target_region = $target_region,
           mop.target_property = $target_property,
           mop.target_program = $target_program,
           mop.synced_at = datetime()
       WITH mop
       MATCH (prog:Program {stdb_id: $program_id})
       MERGE (mop)-[:BELONGS_TO]->(prog)`,
      {
        id: Number(m.id),
        program_id: Number(m.program_id),
        rank_tag: m.rank_tag ?? "",
        window_kind: m.window?.kind ?? null,
        window_value: m.window?.value ?? null,
        target_code: m.target?.code ?? null,
        target_region: m.target?.region ?? null,
        target_property: m.target?.property ?? null,
        target_program: m.target?.program ?? null,
      }
    );
    console.log(`    ✓ MetaOp ${m.id}: ${m.rank_tag} ${m.target?.code ?? ""}@${m.target?.region ?? ""}`);
  }

  // ── Convergence ──
  const convs = await stdbSql("SELECT * FROM conv");
  console.log(`\n  Convergence: ${convs.length}`);
  for (const c of convs) {
    await cypher(
      `MERGE (cv:Conv {stdb_id: $id})
       SET cv.program_id = $program_id,
           cv.kind = $kind,
           cv.signal_code = $signal_code,
           cv.signal_region = $signal_region,
           cv.diagnosis = $diagnosis,
           cv.timeframe = $timeframe,
           cv.predicted = $predicted,
           cv.rationale = $rationale,
           cv.flag_type = $flag_type,
           cv.flag_expr = $flag_expr,
           cv.synced_at = datetime()
       WITH cv
       MATCH (prog:Program {stdb_id: $program_id})
       MERGE (cv)-[:BELONGS_TO]->(prog)`,
      {
        id: Number(c.id),
        program_id: Number(c.program_id),
        kind: c.kind ?? "",
        signal_code: c.signal_code ?? null,
        signal_region: c.signal_region ?? null,
        diagnosis: c.diagnosis ?? null,
        timeframe: c.timeframe ?? null,
        predicted: c.predicted ?? null,
        rationale: c.rationale ?? null,
        flag_type: c.flag_type ?? null,
        flag_expr: c.flag_expr ?? null,
      }
    );
    console.log(`    ✓ Conv ${c.id}: ${c.kind} ${c.signal_code ?? ""}`);
  }

  // ── Summary ──
  console.log("\n  ── Summary ──");
  console.log(`  Programs:  ${programs.length}`);
  console.log(`  Nodes:     ${nodes.length}`);
  console.log(`  Edges:     ${edges.length}`);
  console.log(`  Tensors:   ${tensors.length}`);
  console.log(`  Diags:     ${diags.length}`);
  console.log(`  DeltaOps:  ${deltas.length}`);
  console.log(`  MetaOps:   ${metas.length}`);
  console.log(`  Conv:      ${convs.length}`);
  console.log("\n  Sync complete ✓");
}

// ── Helpers ─────────────────────────────────────────────────────────────────

function kindToLabel(kind) {
  if (!kind) return null;
  const prefix = kind.split(".")[0];
  const map = {
    L: "Ligand",
    R: "Receptor",
    K: "Kinase",
    N: "Neuron",
    G: "Glia",
    T: "Transporter",
    E: "Enzyme",
  };
  return map[prefix] ?? null;
}

function edgeTypeToRel(edgeType, rankTag) {
  if (rankTag === "R2") return "PROTOCOL";
  if (!edgeType) return "CONNECTS";
  const map = {
    "→": "EXCITES",
    "⊣": "INHIBITS",
    "⇌": "MODULATES",
    "↺": "FEEDBACK",
    "⊸": "GATES",
  };
  return map[edgeType] ?? "CONNECTS";
}

sync().catch((err) => {
  console.error("\nSync failed:", err.message);
  process.exit(1);
});
