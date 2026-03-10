import { callReducer } from './client';

// SpacetimeDB expects Option<T> as {"some": value} or {"none": []}
function opt<T>(val: T | null | undefined): { some: T } | { none: [] } {
  return val != null ? { some: val } : { none: [] };
}

export async function createProgram(
  name: string,
  phase: string | null,
  domains: string[]
) {
  return callReducer('create_program', [name, opt(phase), domains]);
}

export async function storeRawBnf(
  programId: number,
  layer: string,
  text: string
) {
  return callReducer('store_raw_bnf', [programId, layer, text]);
}

export async function addNode(
  programId: number,
  code: string,
  kind: string,
  region: string | null,
  rankTag: string,
  state: { sym: string; delta_sign: string | null; delta_val: number | null } | null,
  isRoot: boolean,
  fieldOps: string[] = [],
  props: { k: string; v: string }[] = [],
) {
  // Wrap Option fields in sum type format
  const stateArg = state ? {
    some: {
      sym: state.sym,
      delta_sign: opt(state.delta_sign),
      delta_val: opt(state.delta_val),
    }
  } : { none: [] };

  return callReducer('add_node', [
    programId, code, kind, opt(region), rankTag,
    stateArg, { none: [] }, fieldOps, props, isRoot,
  ]);
}

export async function addEdge(
  programId: number,
  sourceId: number,
  targetId: number,
  rankTag: string,
  edgeType: string | null,
  coeff: number,
  gate: { code: string; region: string; mode: string } | null = null,
  protoLabel: string | null = null,
  chain: string | null = null,
  chainPos: number | null = null,
  ringId: string | null = null,
) {
  return callReducer('add_edge', [
    programId, sourceId, targetId, rankTag,
    opt(edgeType), coeff,
    gate ? { some: gate } : { none: [] },
    { none: [] },          // protocol — not parsed from BNF, set via R2 separately
    opt(protoLabel),
    opt(chain),
    opt(chainPos),
    opt(ringId),
  ]);
}

export async function addTensor(
  programId: number,
  conditions: { code: string; region: string; state: string; negated: boolean }[],
  logic: string,
  effect: { code: string; region: string; action: string; value: number | null },
  label: string | null
) {
  const effectArg = {
    code: effect.code,
    region: effect.region,
    action: effect.action,
    value: opt(effect.value),
  };
  return callReducer('add_tensor', [programId, conditions, logic, effectArg, opt(label)]);
}

export async function reconstruct(programId: number) {
  return callReducer('reconstruct', [programId]);
}

export async function tickProgram(programId: number) {
  return callReducer('tick', [programId]);
}
