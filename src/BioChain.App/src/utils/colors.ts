export const LAYER_COLORS = {
  neurotransmitter: '#3b82f6',
  hormone: '#10b981',
  peptide: '#8b5cf6',
} as const;

export type Layer = keyof typeof LAYER_COLORS;

export function getLayerColor(layer: string): string {
  return LAYER_COLORS[layer as Layer] ?? '#6b7280';
}

export const CHART_PALETTE = [
  '#6366f1', '#3b82f6', '#10b981', '#8b5cf6',
  '#f59e0b', '#ef4444', '#06b6d4', '#ec4899',
  '#84cc16', '#f97316',
];

export const STATUS_COLORS = {
  success: '#22c55e',
  warning: '#f59e0b',
  danger: '#ef4444',
  info: '#06b6d4',
  muted: '#64748b',
} as const;
