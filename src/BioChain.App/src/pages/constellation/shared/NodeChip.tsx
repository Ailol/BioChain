import { TYPE_COLORS, stateIcon } from '@/types/constellation';
import type { ConstellationNode } from '@/types/constellation';

interface NodeChipProps {
  code: string;
  label: string;
  node?: ConstellationNode;
  onClick?: () => void;
  active?: boolean;
}

export function NodeChip({ code, label, node, onClick, active }: NodeChipProps) {
  const bg = node ? (TYPE_COLORS[node.type] ?? '#64748b') : '#1e293b';
  return (
    <span
      onClick={onClick}
      style={{
        padding: '2px 6px', borderRadius: 4,
        fontSize: 9, fontFamily: 'monospace',
        background: active ? bg + '30' : '#1e293b',
        color: active ? bg : '#94a3b8',
        border: active ? `1px solid ${bg}40` : '1px solid transparent',
        cursor: onClick ? 'pointer' : 'default',
        transition: 'all 0.15s',
      }}
    >
      {label !== code ? `${label} (${code})` : code}
      {node && node.state && node.state !== 'unknown' && ` ${stateIcon(node.state)}`}
    </span>
  );
}
