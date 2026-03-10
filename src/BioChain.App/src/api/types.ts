// Types matching the Rust SpacetimeDB module

export interface Program {
  id: number;
  name: string;
  phase: string | null;
  domains: string[];
  tick: number;
  raw_base: string | null;
  raw_plasticity: string | null;
  raw_meta: string | null;
  raw_convergence: string | null;
  created_at: string;
}

export interface NodeState {
  sym: string;
  delta_sign: string | null;
  delta_val: number | null;
}

export interface IntegInput {
  code: string;
  region: string;
  weight: number;
  w_type: string;
}

export interface IntegOutput {
  code: string;
  region: string;
  mode: string;
  threshold: string | null;
}

export interface Integration {
  inputs: IntegInput[];
  output: IntegOutput;
}

export interface Node {
  id: number;
  program_id: number;
  code: string;
  kind: string;
  region: string | null;
  rank_tag: string;
  state: NodeState | null;
  integ: Integration | null;
  field_ops: string[];
  props: Kv[];
  is_root: boolean;
}

export interface GateSpec {
  code: string;
  region: string;
  mode: string;
}

export interface ProtocolSpec {
  gain: number | null;
  polarity: string | null;
  tau_class: string | null;
  tau_value: string | null;
  gate: string | null;
  coupling: string | null;
}

export interface Edge {
  id: number;
  program_id: number;
  source_id: number;
  target_id: number;
  rank_tag: string;
  edge_type: string | null;
  coeff: number;
  gate: GateSpec | null;
  protocol: ProtocolSpec | null;
  proto_label: string | null;
  chain: string | null;
  chain_pos: number | null;
  ring_id: string | null;
}

export interface TensorCond {
  code: string;
  region: string;
  state: string;
  negated: boolean;
}

export interface TensorEffect {
  code: string;
  region: string;
  action: string;
  value: number | null;
}

export interface Tensor {
  id: number;
  program_id: number;
  conditions: TensorCond[];
  logic: string;
  effect: TensorEffect;
  label: string | null;
}

export interface Kv {
  k: string;
  v: string;
}

export interface DeltaOp {
  id: number;
  program_id: number;
  rank_tag: string;
  trigger_code: string;
  trigger_region: string;
  trigger_state: string;
  target_code: string;
  target_region: string;
  change: { property: string; before: string; after: string };
  tau: string;
  tensor_expr: string | null;
}

export interface MetaOp {
  id: number;
  program_id: number;
  rank_tag: string;
  window: { kind: string; value: string };
  target: { code: string; region: string; property: string; program: string };
}

export interface Conv {
  id: number;
  program_id: number;
  kind: string;
  signal_code: string | null;
  signal_region: string | null;
  diagnosis: string | null;
  timeframe: string | null;
  predicted: string | null;
  rationale: string | null;
  flag_type: string | null;
  flag_expr: string | null;
}
