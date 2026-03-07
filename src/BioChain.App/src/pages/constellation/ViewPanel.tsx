import type { ReactNode } from 'react';

interface ViewPanelProps {
  title: string;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function ViewPanel({ title, icon, children }: ViewPanelProps) {
  return (
    <div style={{
      display: 'flex', flexDirection: 'column',
      background: '#0a0a12', borderRadius: 12,
      border: '1px solid #1e293b', overflow: 'hidden',
      minHeight: 0, // important for grid child shrink
    }}>
      {/* Header */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6,
        padding: '6px 10px', borderBottom: '1px solid #1e293b',
        background: 'rgba(15,23,42,0.5)',
      }}>
        {icon}
        <span style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
          {title}
        </span>
      </div>
      {/* Content */}
      <div style={{ flex: 1, overflow: 'auto', position: 'relative', minHeight: 0 }}>
        {children}
      </div>
    </div>
  );
}
