const colorMap: Record<string, string> = {
  functional: '#22c55e', compensated: '#f59e0b', impaired: '#f97316',
  dysfunctional: '#ef4444', collapsed: '#7f1d1d',
  healthy: '#22c55e', stressed: '#f59e0b', dysregulated: '#ef4444',
};

export function StatusBadge({ status }: { status: string }) {
  const c = colorMap[status.toLowerCase()] ?? '#64748b';
  return (
    <span style={{
      padding: '2px 8px', borderRadius: 4, fontSize: 9, fontWeight: 600,
      background: c + '25', color: c, textTransform: 'uppercase',
    }}>
      {status}
    </span>
  );
}
