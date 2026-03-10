/**
 * BNF Parser v2 — tokenizer + state-machine for BioChain BNF text.
 *
 * Architecture:
 *   1. Line classifier — detects section headers, deltas, integration, protocol,
 *      tensor, meta, diag, and chain lines
 *   2. Chain tokenizer — converts chain text into a flat token stream
 *   3. Chain parser — state machine with branch stack, merge set, ring tracking
 *   4. Specialized parsers — R1 integration, R2 protocol, R3 tensor, Δ, Meta
 *
 * Identity rule: Same (kind, code, region) across the entire program = same node.
 * Rank-locked operators: ∫ only R1, ⊲ only R2, ⊗ only R3.
 */

// ─── Output types ───────────────────────────────────────────────

export interface ParsedNode {
  code: string;
  kind: string;
  region: string | null;
  rank: string;
  sym: string | null;
  symVal: number | null;
  isRoot: boolean;
  deltaSign: string | null;
  deltaVal: number | null;
  fieldOps: string[];
  props: { k: string; v: string }[];
}

export interface ParsedEdge {
  sourceKey: string;
  targetKey: string;
  edgeType: string;
  coeff: number;
  rank: string;
  gate: { code: string; region: string; mode: string } | null;
  ringId: string | null;
  chain: string | null;
  chainPos: number | null;
  protoLabel: string | null;
}

export interface ParsedTensor {
  conditions: { code: string; region: string; state: string; negated: boolean }[];
  logic: string;
  effect: { code: string; region: string; action: string; value: number | null };
  label: string | null;
}

export interface ParsedIntegration {
  unitKey: string;
  inputs: { code: string; region: string; weight: number; wType: string }[];
  output: { code: string; region: string; mode: string; threshold: string | null };
}

export interface ParsedDelta {
  rank: string;
  triggerCode: string;
  triggerRegion: string;
  triggerState: string;
  targetCode: string;
  targetRegion: string;
  change: { property: string; before: string; after: string };
  tau: string;
}

export interface ParsedMeta {
  rank: string;
  operator: string;
  window: { kind: string; value: string };
  target: { code: string; region: string; property: string; program: string };
}

export interface ParseResult {
  nodes: ParsedNode[];
  edges: ParsedEdge[];
  tensors: ParsedTensor[];
  integrations: ParsedIntegration[];
  deltas: ParsedDelta[];
  metas: ParsedMeta[];
  diags: { kind: string; expr: string }[];
  warnings: string[];
  errors: string[];
}

// ─── Constants ──────────────────────────────────────────────────

// Characters in BNF codes: word chars, period (DA.release), hyphens (CRH-R1),
// superscripts (Ca²⁺, Cl⁻), greek (α, β, κ, Δ, γ)
// Hyphen at END to avoid range issues when .source is interpolated into new RegExp()
const CODE_CHARS = /[\w.+²·κα!βΔγ⁺⁻\-]/;

function nodeKey(kind: string, code: string, region: string | null): string {
  return region ? `${kind}:${code}@${region}` : `${kind}:${code}`;
}

// ─── Token types ────────────────────────────────────────────────

type Token =
  | { type: 'NODE'; raw: string }
  | { type: 'EDGE'; op: string }
  | { type: 'GATED_EDGE' }
  | { type: 'BIND' }
  | { type: 'PAREN_OPEN' }
  | { type: 'PAREN_CLOSE' }
  | { type: 'MERGE' }
  | { type: 'RING_OPEN'; id: string; sign: string }
  | { type: 'RING_CLOSE'; id: string }
  | { type: 'ROOT' }
  | { type: 'TERMINAL' }
  | { type: 'NEWLINE' };

// Edge operators ordered by length (longest first for priority matching)
const EDGE_OPS = ['→!', '⊣!', '=>', '~>', '|>', '→', '⊣', '⇌', '⊃', '⊂', '←', '↺', '⊸'];

// ─── Tokenizer ──────────────────────────────────────────────────

function tokenizeChain(input: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  const len = input.length;

  while (i < len) {
    const ch = input[i];
    const rest = input.slice(i);

    // Skip spaces and tabs (but not newlines)
    if (ch === ' ' || ch === '\t') { i++; continue; }

    // Skip carriage return
    if (ch === '\r') { i++; continue; }

    // Newline — significant inside branches
    if (ch === '\n') {
      tokens.push({ type: 'NEWLINE' });
      i++;
      continue;
    }

    // Node block: {...} (handle nested braces)
    if (ch === '{') {
      let depth = 1;
      let j = i + 1;
      while (j < len && depth > 0) {
        if (input[j] === '{') depth++;
        else if (input[j] === '}') depth--;
        j++;
      }
      tokens.push({ type: 'NODE', raw: input.slice(i, j) });
      i = j;
      continue;
    }

    // Ring open: «id[±]  e.g. «1⁺ or «2⁻ or «1+
    if (ch === '«') {
      const rm = rest.match(/^«(\w+)([⁺⁻+\-])?/);
      if (rm) {
        const sign = (rm[2] === '⁺' || rm[2] === '+') ? '+' :
                     (rm[2] === '⁻' || rm[2] === '-') ? '-' : '+';
        tokens.push({ type: 'RING_OPEN', id: rm[1], sign });
        i += rm[0].length;
      } else { i++; }
      continue;
    }

    // Ring close: »id  e.g. »1
    if (ch === '»') {
      const rm = rest.match(/^»(\w+)/);
      if (rm) {
        tokens.push({ type: 'RING_CLOSE', id: rm[1] });
        i += rm[0].length;
      } else { i++; }
      continue;
    }

    // Root marker
    if (ch === '⊙') { tokens.push({ type: 'ROOT' }); i++; continue; }

    // Terminal marker
    if (ch === '⊘') { tokens.push({ type: 'TERMINAL' }); i++; continue; }

    // Parens
    if (ch === '(') { tokens.push({ type: 'PAREN_OPEN' }); i++; continue; }
    if (ch === ')') { tokens.push({ type: 'PAREN_CLOSE' }); i++; continue; }

    // Merge
    if (ch === '&') { tokens.push({ type: 'MERGE' }); i++; continue; }

    // Gated edge: →? (must check before →)
    if (rest.startsWith('→?')) {
      tokens.push({ type: 'GATED_EDGE' });
      i += 2;
      continue;
    }

    // Edge operators (longest first)
    let matched = false;
    for (const op of EDGE_OPS) {
      if (rest.startsWith(op)) {
        tokens.push({ type: 'EDGE', op });
        i += op.length;
        matched = true;
        break;
      }
    }
    if (matched) continue;

    // Bind: standalone ?
    if (ch === '?') { tokens.push({ type: 'BIND' }); i++; continue; }

    // Skip any other character (field ops, decorators, etc. — parsed inside nodes)
    i++;
  }

  return tokens;
}

// ─── Node internal parser ───────────────────────────────────────

function parseNodeRaw(raw: string): ParsedNode | null {
  // Remove outer braces
  const inner = raw.replace(/^\{|\}$/g, '').trim();
  if (!inner) return null;

  // Pattern: KIND:CODE(props)[state]@REGION field_ops...
  const m = inner.match(
    new RegExp(
      `^([\\w.]+):(${CODE_CHARS.source}+)` +   // kind:code (GREEDY)
      `(?:\\(([^)]*)\\))?` +                      // optional (props)
      `(?:\\[([^\\]]*)\\])?` +                    // optional [state]
      `(?:@(\\w+))?`                              // optional @region
    )
  );
  if (!m) return null;

  const [fullMatch, kind, code, propsStr, stateStr, region] = m;

  // Parse state: ↑, ↓↓, ≈, ↑:0.9, ↓:0.3 Δ+0.2, ~, ⊘, ●
  let sym: string | null = null;
  let symVal: number | null = null;
  let deltaSign: string | null = null;
  let deltaVal: number | null = null;

  if (stateStr) {
    const symMatch = stateStr.match(/^([↑↓≈~⊘●]+)/);
    if (symMatch) sym = symMatch[1];
    const valMatch = stateStr.match(/^[↑↓≈~⊘●]+:([\d.]+)/);
    if (valMatch) symVal = parseFloat(valMatch[1]);
    const dMatch = stateStr.match(/Δ([+-])([\d.]+)/);
    if (dMatch) { deltaSign = dMatch[1]; deltaVal = parseFloat(dMatch[2]); }
  }

  // Parse props: (coup:Gs,st:down)
  const props: { k: string; v: string }[] = [];
  if (propsStr) {
    for (const p of propsStr.split(',')) {
      const kv = p.trim().split(':');
      if (kv.length >= 2) props.push({ k: kv[0].trim(), v: kv.slice(1).join(':').trim() });
    }
  }

  // Parse field ops from remainder: ∇→NAc, ∇²syn, ∇²vol, ∇·+, ∇×1⁺, -∇φ:...
  const fieldOps: string[] = [];
  const remainder = inner.slice(fullMatch.length).trim();
  if (remainder) {
    const ops = remainder.match(/(?:∇[→²·×][^\s]*|-∇φ:[^\s]*)/g);
    if (ops) fieldOps.push(...ops);
  }

  return {
    code, kind, region: region || null, rank: 'R0',
    sym, symVal, isRoot: false, deltaSign, deltaVal, fieldOps, props,
  };
}

// ─── Chain parser (state machine) ───────────────────────────────

function parseChainTokens(
  tokens: Token[],
  rank: string,
  nodeMap: Map<string, ParsedNode>,
  edges: ParsedEdge[],
  chainLabel: string | null,
  warnings: string[],
) {
  const branchStack: string[] = [];
  let currentNode: string | null = null;
  let pendingEdge: string | null = null;
  let nextIsRoot = false;
  let currentRingId: string | null = null;
  const mergeSet: string[] = [];
  let isGated = false;
  let gateNode: { code: string; region: string; mode: string } | null = null;
  let bindingReceptorKey: string | null = null;
  let chainPos = 0;

  /** Resolve node identity: reuse existing or create new. Merge state. */
  function resolveOrUpdate(node: ParsedNode): string {
    node.rank = rank;
    if (nextIsRoot) { node.isRoot = true; nextIsRoot = false; }
    const key = nodeKey(node.kind, node.code, node.region);

    if (nodeMap.has(key)) {
      const ex = nodeMap.get(key)!;
      if (node.sym && !ex.sym) ex.sym = node.sym;
      if (node.symVal != null && ex.symVal == null) ex.symVal = node.symVal;
      if (node.isRoot) ex.isRoot = true;
      if (node.deltaSign && !ex.deltaSign) {
        ex.deltaSign = node.deltaSign;
        ex.deltaVal = node.deltaVal;
      }
      for (const op of node.fieldOps) {
        if (!ex.fieldOps.includes(op)) ex.fieldOps.push(op);
      }
      for (const p of node.props) {
        if (!ex.props.some(ep => ep.k === p.k)) ex.props.push(p);
      }
    } else {
      nodeMap.set(key, node);
    }
    return key;
  }

  function emitEdge(
    srcKey: string, tgtKey: string, edgeType: string,
    gate?: typeof gateNode,
  ) {
    if (srcKey === tgtKey) return; // no self-edges
    edges.push({
      sourceKey: srcKey, targetKey: tgtKey, edgeType, coeff: 1.0, rank,
      gate: gate || null, ringId: currentRingId,
      chain: chainLabel, chainPos: chainPos++, protoLabel: null,
    });
  }

  for (const token of tokens) {
    switch (token.type) {

      case 'ROOT':
        nextIsRoot = true;
        break;

      case 'TERMINAL': {
        // Create terminal pseudo-node and edge to it
        const termKey = '⊘:⊘';
        if (!nodeMap.has(termKey)) {
          nodeMap.set(termKey, {
            code: '⊘', kind: '⊘', region: null, rank,
            sym: null, symVal: null, isRoot: false,
            deltaSign: null, deltaVal: null, fieldOps: [], props: [],
          });
        }
        const src = currentNode;
        if (src) emitEdge(src, termKey, pendingEdge || '→');
        pendingEdge = null;
        currentNode = null;
        break;
      }

      case 'NODE': {
        const parsed = parseNodeRaw(token.raw);
        if (!parsed) {
          warnings.push(`Unparseable node: ${token.raw}`);
          break;
        }
        const nk = resolveOrUpdate(parsed);

        // ── Pattern 6: receptor binding ──
        if (bindingReceptorKey != null) {
          emitEdge(nk, bindingReceptorKey, 'bind');
          currentNode = bindingReceptorKey; // chain continues from receptor
          bindingReceptorKey = null;
          break;
        }

        // ── Pattern 5: gated edge — first node is condition ──
        if (isGated && gateNode == null) {
          const gm = token.raw.match(
            /\{(?:[\w.]+:)?([^\[}@]+?)(?:@(\w+))?.*?([><=]+)([^\}]+)\}/
          );
          if (gm) {
            gateNode = { code: gm[1], region: gm[2] || '', mode: `${gm[3]}${gm[4]}` };
          } else {
            const plain = token.raw.replace(/^\{|\}$/g, '').trim();
            gateNode = { code: plain, region: '', mode: '' };
          }
          // Register this as a node too (conditions are signals)
          break; // wait for the actual target
        }

        // ── Pattern 5: gated edge — second node is actual target ──
        if (isGated && gateNode != null) {
          const source = currentNode
            ?? (branchStack.length > 0 ? branchStack[branchStack.length - 1] : null);
          if (source) emitEdge(source, nk, '→', gateNode);
          currentNode = nk;
          isGated = false;
          gateNode = null;
          break;
        }

        // ── Normal edge creation ──
        if (pendingEdge != null) {
          // Pattern 3: merge — fan-in
          if (mergeSet.length > 0) {
            if (currentNode) mergeSet.push(currentNode);
            for (const ms of mergeSet) emitEdge(ms, nk, pendingEdge);
            mergeSet.length = 0;
            pendingEdge = null;
            currentNode = nk;
            break;
          }

          // Determine source: currentNode (chain continuation) or branchStack top
          const source = currentNode
            ?? (branchStack.length > 0 ? branchStack[branchStack.length - 1] : null);

          if (source) emitEdge(source, nk, pendingEdge);
          pendingEdge = null;
        }

        currentNode = nk;
        break;
      }

      case 'EDGE':
        pendingEdge = token.op;
        break;

      case 'GATED_EDGE':
        isGated = true;
        gateNode = null;
        break;

      case 'BIND':
        bindingReceptorKey = currentNode;
        break;

      case 'PAREN_OPEN':
        if (currentNode) {
          branchStack.push(currentNode);
          currentNode = null;
        }
        break;

      case 'PAREN_CLOSE':
        if (branchStack.length > 0) branchStack.pop();
        currentNode = null;
        break;

      case 'MERGE':
        if (currentNode) { mergeSet.push(currentNode); currentNode = null; }
        break;

      case 'RING_OPEN':
        currentRingId = token.id;
        break;

      case 'RING_CLOSE':
        currentRingId = null;
        break;

      case 'NEWLINE':
        // Inside branch: reset chain so next edge branches from source
        if (branchStack.length > 0) currentNode = null;
        break;
    }
  }
}

// ─── R1 Integration parser ──────────────────────────────────────

function parseR1Integration(
  line: string,
  nodeMap: Map<string, ParsedNode>,
  edges: ParsedEdge[],
  integrations: ParsedIntegration[],
) {
  // ∫{N.da:VTA_DA@VTA}←( GLU@VTA:+0.7, GABA@VTA:-0.5, CORT@ADR:×0.4 )→DA@VTA:thr:-45mV
  const unitM = line.match(
    new RegExp(`∫\\{([\\w.]+):(${CODE_CHARS.source}+)@(\\w+)\\}`)
  );
  if (!unitM) return;
  const [, uKind, uCode, uRegion] = unitM;
  const unitKey = nodeKey(uKind, uCode, uRegion);

  if (!nodeMap.has(unitKey)) {
    nodeMap.set(unitKey, {
      code: uCode, kind: uKind, region: uRegion, rank: 'R1',
      sym: null, symVal: null, isRoot: false,
      deltaSign: null, deltaVal: null, fieldOps: [], props: [],
    });
  }

  // Parse inputs: ←( code@region:weight, ... )
  const inputsM = line.match(/←\(\s*([\s\S]*?)\s*\)/);
  const inputs: ParsedIntegration['inputs'] = [];
  if (inputsM) {
    for (const part of inputsM[1].split(',')) {
      const im = part.trim().match(/^([\w.]+)@(\w+):([+\-×]?)([\d.]+)/);
      if (im) {
        const weight = parseFloat(im[4]) * (im[3] === '-' ? -1 : 1);
        const wType = im[3] === '-' ? 'inh' : im[3] === '×' ? 'mod' : 'exc';
        inputs.push({ code: im[1], region: im[2], weight, wType });

        // Create implicit edges: input signal → integration unit
        const inputKey = findNodeByCodeRegion(nodeMap, im[1], im[2]);
        if (inputKey) {
          edges.push({
            sourceKey: inputKey, targetKey: unitKey, edgeType: '→',
            coeff: weight, rank: 'R1',
            gate: null, ringId: null, chain: null, chainPos: null, protoLabel: null,
          });
        }
      }
    }
  }

  // Parse output: →code@region:mode:threshold
  const outM = line.match(/\)→([\w.]+)@(\w+)(?::(\w+))?(?::([^\s]+))?/);
  let output: ParsedIntegration['output'] = {
    code: '', region: '', mode: 'fire', threshold: null,
  };
  if (outM) {
    output = {
      code: outM[1], region: outM[2],
      mode: outM[3] || 'fire',
      threshold: outM[4] || null,
    };
    // Create implicit edge: integration unit → output signal
    const outKey = findNodeByCodeRegion(nodeMap, outM[1], outM[2]);
    if (outKey) {
      edges.push({
        sourceKey: unitKey, targetKey: outKey, edgeType: '→',
        coeff: 1.0, rank: 'R1',
        gate: null, ringId: null, chain: null, chainPos: null, protoLabel: null,
      });
    }
  }

  integrations.push({ unitKey, inputs, output });
}

// ─── R2 Protocol parser ─────────────────────────────────────────

function parseR2Protocol(
  line: string,
  nodeMap: Map<string, ParsedNode>,
  edges: ParsedEdge[],
  warnings: string[],
) {
  // {L.nt:DA[↓]@PFC ∇²vol}⊲{GLU→NMDA@PFC}[gain:×0.6, tau:slow:200ms, coup:vol]
  const parts = line.split('⊲');
  if (parts.length < 2) return;

  // Parse source node
  const srcRaw = parts[0].match(/\{[^}]+\}/)?.[0];
  if (!srcRaw) return;
  const srcNode = parseNodeRaw(srcRaw);
  if (!srcNode) return;
  srcNode.rank = 'R2';

  const srcKey = nodeKey(srcNode.kind, srcNode.code, srcNode.region);
  if (!nodeMap.has(srcKey)) {
    nodeMap.set(srcKey, srcNode);
  }

  // Parse protocol target: {GLU→NMDA@PFC} or {DA.synthesis@VTA} or {ClockGen@SCN}
  const tgtMatch = parts[1].match(/\{([^}]+)\}/);
  if (!tgtMatch) return;
  const tgtInner = tgtMatch[1];

  // Extract gain coefficient
  const gainMatch = parts[1].match(/gain:×([\d.]+)/);
  const coeff = gainMatch ? parseFloat(gainMatch[1]) : 1.0;

  // Resolve target: "GLU→NMDA@PFC" → find NMDA@PFC node
  const arrowM = tgtInner.match(/([\w.]+)→([\w.+²·κα!βΔγ⁺⁻\-]+)@(\w+)/);
  const simpleM = !arrowM ? tgtInner.match(/([\w.+²·κα!βΔγ⁺⁻\-]+)@(\w+)/) : null;

  let targetKey: string | null = null;
  let protoLabel: string | null = null;

  if (arrowM) {
    protoLabel = `${arrowM[1]}→${arrowM[2]}@${arrowM[3]}`;
    targetKey = findNodeByCodeRegion(nodeMap, arrowM[2], arrowM[3]);
  } else if (simpleM) {
    protoLabel = `${simpleM[1]}@${simpleM[2]}`;
    targetKey = findNodeByCodeRegion(nodeMap, simpleM[1], simpleM[2]);
  }

  if (targetKey) {
    edges.push({
      sourceKey: srcKey, targetKey, edgeType: '⊲', coeff, rank: 'R2',
      gate: null, ringId: null, chain: null, chainPos: null,
      protoLabel,
    });
  } else {
    warnings.push(`R2 protocol target not found: ${tgtInner}`);
  }
}

// ─── R3 Tensor parser ───────────────────────────────────────────

function parseTensorLine(line: string): ParsedTensor | null {
  // ⊗( {code@region}>=state ∧ ... )⟹{target}:action:value
  const m = line.match(/⊗\(\s*(.*?)\s*\)⟹(.+)/);
  if (!m) return null;
  const [, condStr, effectStr] = m;

  const conditions: ParsedTensor['conditions'] = [];
  const condParts = condStr.split(/\s*[∧∨]\s*/);
  for (const cp of condParts) {
    const negated = cp.includes('¬');
    const cm = cp.match(
      new RegExp(`\\{(?:[\\w.]+:)?(${CODE_CHARS.source}+)@(\\w+)\\}([><=]+)([\\w↑↓≈]+)`)
    );
    if (cm) conditions.push({ code: cm[1], region: cm[2], state: cm[4], negated });
  }

  const em = effectStr.match(
    new RegExp(`\\{(?:[\\w.]+:)?(${CODE_CHARS.source}+)@(\\w+)\\}:(\\w+)(?::([\\d.]+))?`)
  );
  if (!em) return null;

  return {
    conditions,
    logic: condStr.includes('∧') ? 'AND' : condStr.includes('∨') ? 'OR' : 'AND',
    effect: { code: em[1], region: em[2], action: em[3], value: em[4] ? parseFloat(em[4]) : null },
    label: null,
  };
}

// ─── Delta parser ───────────────────────────────────────────────

function parseDeltaSeed(
  line: string,
  nodeMap: Map<string, ParsedNode>,
): boolean {
  // Δ(KIND:CODE@REGION)=±value
  const m = line.match(
    new RegExp(`^Δ\\(([\\w.]+):(${CODE_CHARS.source}+)@(\\w+)\\)=([+-])([\\d.]+)`)
  );
  if (!m) return false;
  const [, kind, code, region, sign, val] = m;
  const key = nodeKey(kind, code, region);

  if (nodeMap.has(key)) {
    const ex = nodeMap.get(key)!;
    ex.isRoot = true;
    ex.deltaSign = sign;
    ex.deltaVal = parseFloat(val);
  } else {
    nodeMap.set(key, {
      code, kind, region, rank: 'R0', sym: null, symVal: null,
      isRoot: true, deltaSign: sign, deltaVal: parseFloat(val),
      fieldOps: [], props: [],
    });
  }
  return true;
}

function parseDeltaPlasticity(line: string): ParsedDelta | null {
  // Δ@R2: {KIND:CODE[state]@REGION} ≫ {⊲:target(prop:before→after)} [τ:time]
  const m = line.match(
    /^Δ@(R\d):\s*\{(?:[\w.]+:)?([\w.+²·κα!βΔγ⁺⁻\-]+)(?:\[([^\]]*)\])?@(\w+)\}\s*≫\s*\{(?:⊲:)?([\w.→⁺⁻\-]+)@(\w+)(?:\(([^)]*)\))?\}\s*(?:\[τ:([^\]]+)\])?/
  );
  if (!m) return null;
  const [, rank, trigCode, trigState, trigRegion, tgtCode, tgtRegion, changeStr, tau] = m;

  let change = { property: '', before: '', after: '' };
  if (changeStr) {
    const cm = changeStr.match(/([\w.]+):([\w.×]+)→([\w.×]+)/);
    if (cm) change = { property: cm[1], before: cm[2], after: cm[3] };
  }

  return {
    rank,
    triggerCode: trigCode, triggerRegion: trigRegion,
    triggerState: trigState || '',
    targetCode: tgtCode, targetRegion: tgtRegion,
    change, tau: tau || '',
  };
}

// ─── Meta parser ────────────────────────────────────────────────

function parseMetaLine(line: string): ParsedMeta | null {
  // σ̃[window]( {KIND:code@region}(property:program) )
  const m = line.match(
    /^([σ∫⊲⊗]̃)\[([^\]]+)\]\(\s*\{(?:[\w.]+:)?([\w.+²·κα!βΔγ⁺⁻\-]+)@(\w+)\}(?:\(([^)]+)\))?\s*\)/
  );
  if (!m) return null;
  const [, operator, windowStr, code, region, propsStr] = m;

  let window = { kind: '', value: '' };
  const wm = windowStr.match(/^(\w+):(.+)/);
  if (wm) window = { kind: wm[1], value: wm[2] };
  else window = { kind: 'condition', value: windowStr };

  let target = { code, region, property: '', program: '' };
  if (propsStr) {
    const pm = propsStr.match(/([\w.]+):([\w.]+→[\w.]+)/);
    if (pm) target = { code, region, property: pm[1], program: pm[2] };
  }

  // Determine rank from operator
  const rankMap: Record<string, string> = { 'σ̃': 'M0', '∫̃': 'M1', '⊲̃': 'M2', '⊗̃': 'M3' };
  const rank = rankMap[operator] || 'M0';

  return { rank, operator, window, target };
}

// ─── Node lookup helper ─────────────────────────────────────────

/** Find existing node by code@region, ignoring kind prefix (identity rule) */
function findNodeByCodeRegion(
  nodeMap: Map<string, ParsedNode>, code: string, region: string,
): string | null {
  // Exact match
  for (const [key, node] of nodeMap) {
    if (node.code === code && node.region === region) return key;
  }
  // Normalized match (GABA-A ↔ GABA_A)
  const norm = code.replace(/[-_]/g, '');
  for (const [key, node] of nodeMap) {
    if (node.code.replace(/[-_]/g, '') === norm && node.region === region) return key;
  }
  return null;
}

// ─── Edge deduplication ─────────────────────────────────────────

function deduplicateEdges(edges: ParsedEdge[]): ParsedEdge[] {
  const seen = new Set<string>();
  return edges.filter(e => {
    const key = `${e.sourceKey}|${e.targetKey}|${e.edgeType}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

// ─── Chain label inference ──────────────────────────────────────

function inferChainLabel(line: string): string | null {
  // Try to find root or first significant node
  const m = line.match(/\{(?:[\w.]+:)?([\w.]+)@(\w+)/);
  if (m) return `${m[1].toLowerCase()}_${m[2].toLowerCase()}`;
  return null;
}

// ─── Validation ─────────────────────────────────────────────────

function validate(
  nodeMap: Map<string, ParsedNode>,
  edges: ParsedEdge[],
  tensors: ParsedTensor[],
): string[] {
  const warnings: string[] = [];

  // Root nodes should have Δ
  for (const [, node] of nodeMap) {
    if (node.isRoot && node.deltaSign == null && node.deltaVal == null) {
      warnings.push(`Root node ${node.code}@${node.region} has no Δ value`);
    }
  }

  // Check for orphan nodes (no edges at all)
  const connectedKeys = new Set<string>();
  for (const e of edges) {
    connectedKeys.add(e.sourceKey);
    connectedKeys.add(e.targetKey);
  }
  for (const [key, node] of nodeMap) {
    if (key === '⊘:⊘') continue; // terminal is ok
    if (!connectedKeys.has(key)) {
      warnings.push(`Orphan node (no edges): ${node.code}@${node.region}`);
    }
  }

  // Tensor conditions should reference existing nodes
  for (const t of tensors) {
    for (const c of t.conditions) {
      if (!findNodeByCodeRegion(nodeMap, c.code, c.region)) {
        warnings.push(`Tensor condition references unknown node: ${c.code}@${c.region}`);
      }
    }
  }

  return warnings;
}

// ─── Main parser ────────────────────────────────────────────────

export function parseBnf(raw: string): ParseResult {
  const nodeMap = new Map<string, ParsedNode>();
  const edges: ParsedEdge[] = [];
  const tensors: ParsedTensor[] = [];
  const integrations: ParsedIntegration[] = [];
  const deltas: ParsedDelta[] = [];
  const metas: ParsedMeta[] = [];
  const diags: { kind: string; expr: string }[] = [];
  const warnings: string[] = [];
  const errors: string[] = [];

  let currentRank = 'R0';
  const chainLines: string[] = [];  // accumulate chain lines for batch processing

  const lines = raw.split('\n');

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) continue;

    // Skip comments and domain declarations
    if (trimmed.startsWith('#') || trimmed.startsWith('@domain')) continue;

    // Section headers: @R0, @R1, @R2, @R3, @Δ, @M0-@M3
    if (/^@(R[0-3]|Δ|M[0-3])$/.test(trimmed)) {
      // Flush accumulated chain lines before switching section
      if (chainLines.length > 0) {
        processChainBlock(chainLines.join('\n'), currentRank, nodeMap, edges, warnings);
        chainLines.length = 0;
      }
      currentRank = trimmed.slice(1);
      continue;
    }

    // Δ seed lines: Δ(KIND:CODE@REGION)=±value
    if (trimmed.startsWith('Δ(')) {
      if (parseDeltaSeed(trimmed, nodeMap)) continue;
    }

    // Δ plasticity lines: Δ@R2: ...
    if (trimmed.startsWith('Δ@')) {
      const delta = parseDeltaPlasticity(trimmed);
      if (delta) { deltas.push(delta); continue; }
    }

    // R3 Tensor: ⊗(...)⟹...
    if (trimmed.startsWith('⊗')) {
      const tensor = parseTensorLine(trimmed);
      if (tensor) { tensors.push(tensor); continue; }
    }

    // Diagnostic lines: Σ∇·, ◈, ⚡
    if (/^[Σ◈⚡]/.test(trimmed)) {
      const kind = trimmed.startsWith('Σ') ? 'flux'
        : trimmed.startsWith('◈') ? 'composite' : 'flag';
      diags.push({ kind, expr: trimmed });
      continue;
    }

    // R1 Integration: ∫{...}←(...)→...
    if (trimmed.startsWith('∫')) {
      parseR1Integration(trimmed, nodeMap, edges, integrations);
      continue;
    }

    // R2 Protocol: contains ⊲
    if (trimmed.includes('⊲')) {
      parseR2Protocol(trimmed, nodeMap, edges, warnings);
      continue;
    }

    // Meta operators: σ̃, ∫̃, ⊲̃, ⊗̃
    if (/^[σ∫⊲⊗]̃/.test(trimmed)) {
      const meta = parseMetaLine(trimmed);
      if (meta) { metas.push(meta); continue; }
    }

    // Plasticity: ≫≫ or ≫ (without Δ@ prefix)
    if (trimmed.includes('≫')) {
      // For now, store as diagnostic
      diags.push({ kind: 'plasticity', expr: trimmed });
      continue;
    }

    // Everything else: accumulate as chain line
    chainLines.push(trimmed);
  }

  // Flush remaining chain lines
  if (chainLines.length > 0) {
    processChainBlock(chainLines.join('\n'), currentRank, nodeMap, edges, warnings);
  }

  // Post-processing: validation
  const validationWarnings = validate(nodeMap, edges, tensors);
  warnings.push(...validationWarnings);

  return {
    nodes: Array.from(nodeMap.values()),
    edges: deduplicateEdges(edges),
    tensors, integrations, deltas, metas, diags, warnings, errors,
  };
}

/** Tokenize and parse a block of chain lines */
function processChainBlock(
  text: string,
  rank: string,
  nodeMap: Map<string, ParsedNode>,
  edges: ParsedEdge[],
  warnings: string[],
) {
  const chainLabel = inferChainLabel(text);
  const tokens = tokenizeChain(text);
  parseChainTokens(tokens, rank, nodeMap, edges, chainLabel, warnings);
}
