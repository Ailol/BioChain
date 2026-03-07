const colors: Record<string, string> = {
  critical: '#ef4444', high: '#f59e0b', medium: '#38bdf8', low: '#64748b',
};

export function SeverityBadge({ severity }: { severity: string }) {
  const c = colors[severity.toLowerCase()] ?? '#64748b';
  return (
    <span style={{ fontSize: 9, fontWeight: 600, color: c, textTransform: 'uppercase' }}>
      {severity}
    </span>
  );
}
