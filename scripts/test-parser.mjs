// Test the full parseBnf() against real BNF from SpacetimeDB
// Run: npx tsx scripts/test-parser.mjs  (or node --experimental-strip-types)

// ── Inline minimal regex test first ──
const CODE_CHARS = /[\w.+²·κα!βΔγ⁺⁻\-]/;
const re = new RegExp(
  `^([\\w.]+):(${CODE_CHARS.source}+)` +
  `(?:\\(([^)]*)\\))?` +
  `(?:\\[([^\\]]*)\\])?` +
  `(?:@(\\w+))?`
);

const staticTests = [
  '{L.nt:DA[↓:0.3]@VTA}', '{L.nt:GABA[↑]@VTA}', '{R:D1@NAc}',
  '{L.h:CORT[↑↑]@ADR}', '{L.nt:GLU[↑]@PFC}', '{L.p:BDNF[↓]@HPC}',
  '{R:NMDA@PFC}', '{L.nt:5HT[↓]@DRN}', '{E.v:Vm[↓]@VTA}',
  '{2m:cAMP[↓]@NAc}', '{L.cb:AEA[↑]@NAc}', '{L.ni:NO[↑]@PFC}',
  '{Ch.vg:Nav1.6[≈]@VTA}', '{L.nt:ACh[↓]@BF}', '{T:DAT[↑]@VTA}',
  '{N.da:VTA_DA@VTA}', '{Gp:Gi@VTA}', '{K:PKA@NAc}',
  '{L.p:CRH@PVN}', '{R:CRH-R1@Pit}', '{L.h:ACTH@Pit}',
  '{Mt:MAO@VTA}', '{G:CREB@NAc}', '{TF:ΔFosB@NAc}', '{Ph:PP1@NAc}',
];

let pass = 0, fail = 0;
for (const t of staticTests) {
  const inner = t.replace(/^\{|\}$/g, '').trim();
  if (inner.match(re)) pass++; else { fail++; console.log(`  FAIL: ${t}`); }
}
console.log(`Static regex: ${pass} pass, ${fail} fail\n`);

// ── Fetch real BNF from SpacetimeDB ──
console.log('--- Full parser test against real BNF ---');
try {
  const res = await fetch('http://localhost:3000/v1/database/biochain/sql', {
    method: 'POST',
    headers: { 'Content-Type': 'text/plain' },
    body: "SELECT raw_base FROM program LIMIT 1",
  });
  const data = await res.json();

  let rawBase;
  if (Array.isArray(data) && data[0]?.rows) {
    const row = data[0].rows[0];
    // SpacetimeDB rows are arrays of columns; nullable columns are Sum types [0, value] or [1, null]
    rawBase = unwrapSumType(Array.isArray(row) ? row[0] : row);
  }

  function unwrapSumType(val) {
    if (typeof val === 'string') return val;
    if (Array.isArray(val) && val.length === 2 && typeof val[0] === 'number') {
      return typeof val[1] === 'string' ? val[1] : JSON.stringify(val[1]);
    }
    if (typeof val === 'object' && val !== null) return Object.values(val)[0];
    return String(val);
  }
  if (!rawBase) { console.log('No raw_base found in DB'); process.exit(0); }

  console.log(`raw_base: ${rawBase.length} chars, ${rawBase.split('\n').length} lines`);
  console.log('Preview:', rawBase.slice(0, 200), '\n');

  // ── Import the actual parser (TypeScript, needs tsx or strip-types) ──
  let parseBnf;
  try {
    const mod = await import('../src/BioChain.App/src/api/bnfParser.ts');
    parseBnf = mod.parseBnf;
  } catch {
    console.log('Cannot import .ts directly. Testing regex only.\n');
    // Fall back to regex-only test
    const nodeRegex = /\{([^{}]+)\}/g;
    let nm;
    const allTokens = [];
    while ((nm = nodeRegex.exec(rawBase)) !== null) allTokens.push(nm[0]);
    console.log(`Found ${allTokens.length} node tokens`);

    let tokenPass = 0, tokenFail = 0;
    const failedTokens = [];
    for (const tok of allTokens) {
      const inner = tok.replace(/^\{|\}$/g, '').trim();
      const m = inner.match(re);
      if (m && m[2].length > 1) tokenPass++;
      else { tokenFail++; failedTokens.push(tok); }
    }
    console.log(`Token parsing: ${tokenPass} pass, ${tokenFail} fail`);
    if (failedTokens.length > 0) console.log('Failed tokens:', failedTokens.slice(0, 15));
    process.exit(0);
  }

  // ── Full parser test ──
  const result = parseBnf(rawBase);
  console.log('=== parseBnf() results ===');
  console.log(`Nodes:        ${result.nodes.length}`);
  console.log(`Edges:        ${result.edges.length}`);
  console.log(`Tensors:      ${result.tensors.length}`);
  console.log(`Integrations: ${result.integrations.length}`);
  console.log(`Deltas:       ${result.deltas.length}`);
  console.log(`Metas:        ${result.metas.length}`);
  console.log(`Diags:        ${result.diags.length}`);
  console.log(`Warnings:     ${result.warnings.length}`);
  console.log(`Errors:       ${result.errors.length}`);

  // Node breakdown
  const kindCounts = {};
  const rootNodes = [];
  for (const n of result.nodes) {
    kindCounts[n.kind] = (kindCounts[n.kind] || 0) + 1;
    if (n.isRoot) rootNodes.push(`${n.code}@${n.region}`);
  }
  console.log('\nNodes by kind:', kindCounts);
  console.log('Root nodes:', rootNodes);

  // Edge breakdown
  const edgeTypeCounts = {};
  for (const e of result.edges) {
    edgeTypeCounts[e.edgeType] = (edgeTypeCounts[e.edgeType] || 0) + 1;
  }
  console.log('Edges by type:', edgeTypeCounts);

  // Self-edge check
  const selfEdges = result.edges.filter(e => e.sourceKey === e.targetKey);
  console.log(`Self-edges: ${selfEdges.length}`);

  // Show warnings
  if (result.warnings.length > 0) {
    console.log('\nWarnings:');
    for (const w of result.warnings.slice(0, 20)) console.log(`  - ${w}`);
    if (result.warnings.length > 20) console.log(`  ... and ${result.warnings.length - 20} more`);
  }

  // Show some nodes
  console.log('\nSample nodes:');
  for (const n of result.nodes.slice(0, 5)) {
    console.log(`  ${n.kind}:${n.code}@${n.region} [${n.sym || '—'}] root=${n.isRoot} rank=${n.rank} fieldOps=${n.fieldOps.length} props=${n.props.length}`);
  }

  // Show some edges
  console.log('\nSample edges:');
  for (const e of result.edges.slice(0, 5)) {
    console.log(`  ${e.sourceKey} ${e.edgeType} ${e.targetKey} coeff=${e.coeff} ring=${e.ringId || '—'} chain=${e.chain || '—'}`);
  }

} catch (e) {
  console.log('SpacetimeDB not reachable or no data:', e.message);
}
