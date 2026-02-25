import { useState, useEffect } from 'react';
import {
  RadarChart, PolarGrid, PolarAngleAxis, PolarRadiusAxis, Radar,
  AreaChart, Area, BarChart, Bar, XAxis, YAxis, Tooltip,
  ResponsiveContainer, Cell,
} from 'recharts';
import { LoadingSpinner } from '@/components/LoadingSpinner';
import { usePersonStore } from '@/stores/personStore';
import { personalSphereApi } from '@/api/personalSphere';
import type {
  PersonalSphereResponse,
  PersonalSphereInsight,
  PersonalSpherePattern,
  PersonalSphereLeveragePoint,
  PersonalSphereStrength,
  PersonalSphereSystemRadar,
  PersonalSphereEnergyCurve,
} from '@/types';

// ═══ COLOR SYSTEM ═══
const C = {
  bg: '#0a0b0f',
  surface: '#12131a',
  surfaceHover: '#1a1b24',
  border: '#1e2030',
  borderActive: '#2a3050',
  text: '#e8e6e3',
  textMuted: '#6b7084',
  textDim: '#3d4155',
  accent: '#5b8af5',
  accentDim: '#2a3f7a',
  up: '#4ade80',
  upDim: '#1a4a2e',
  down: '#f87171',
  downDim: '#4a1a1a',
  warn: '#fbbf24',
  warnDim: '#4a3a1a',
  purple: '#a78bfa',
  purpleDim: '#2a1f4a',
  cyan: '#22d3ee',
  cyanDim: '#0a3a3f',
  orange: '#fb923c',
  pink: '#f472b6',
};

const GRADIENT_COLORS = [C.accent, C.purple, C.cyan, C.orange, C.pink, C.up];

// ═══ SUB-COMPONENTS ═══
function Card({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <div
      style={{
        background: C.surface,
        border: `1px solid ${C.border}`,
        borderRadius: 12,
        padding: 20,
        ...style,
      }}
    >
      {children}
    </div>
  );
}

function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <h2
      style={{
        fontSize: 16,
        fontWeight: 600,
        color: C.text,
        marginBottom: 16,
        display: 'flex',
        alignItems: 'center',
        gap: 8,
      }}
    >
      {children}
    </h2>
  );
}

// ═══ MAIN COMPONENT ═══
export default function PersonalInsightPage() {
  const { activePerson } = usePersonStore();
  const [data, setData] = useState<PersonalSphereResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!activePerson) return;
    setLoading(true);
    setError(null);
    personalSphereApi
      .get(activePerson)
      .then(setData)
      .catch((err) => setError(err?.response?.data?.error ?? 'Failed to load insights'))
      .finally(() => setLoading(false));
  }, [activePerson]);

  if (!activePerson) {
    return (
      <div style={{ padding: 32, color: C.textMuted, textAlign: 'center' }}>
        Select a person to view personal insights.
      </div>
    );
  }

  if (loading) {
    return (
      <div style={{ padding: 32 }}>
        <LoadingSpinner text="Generating personal insights..." />
      </div>
    );
  }

  if (error) {
    return (
      <div style={{ padding: 32, color: C.down, textAlign: 'center' }}>
        {error}
      </div>
    );
  }

  if (!data) {
    return (
      <div style={{ padding: 32, color: C.textMuted, textAlign: 'center' }}>
        No insight data available yet. Analyze some conversations first.
      </div>
    );
  }

  const {
    coreInsights,
    deepPatterns,
    leveragePoints,
    strengths,
    systemRadar,
    energyCurve,
  } = data;

  return (
    <div
      style={{
        minHeight: '100vh',
        background: C.bg,
        color: C.text,
        fontFamily: "'Inter', -apple-system, sans-serif",
      }}
    >
      {/* Header */}
      <div style={{ padding: '24px 24px 0' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 4 }}>
          <div
            style={{
              width: 10,
              height: 10,
              borderRadius: '50%',
              background: C.accent,
              boxShadow: `0 0 12px ${C.accent}80`,
            }}
          />
          <h1 style={{ fontSize: 22, fontWeight: 700, color: C.text, margin: 0 }}>
            Personal Insight
          </h1>
        </div>
        <p style={{ fontSize: 13, color: C.textMuted, marginLeft: 22 }}>
          AI-generated insights for <strong>{activePerson}</strong> &mdash; powered by layer agents
        </p>
      </div>

      <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20 }}>
        {/* Core Insights Grid */}
        {coreInsights.length > 0 && (
          <div>
            <SectionTitle>Core Insights</SectionTitle>
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
                gap: 14,
              }}
            >
              {coreInsights.map((insight, i) => (
                <InsightCard key={insight.id} insight={insight} index={i} />
              ))}
            </div>
          </div>
        )}

        {/* Two-column: Deep Patterns + Strengths */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))',
            gap: 14,
          }}
        >
          {/* Deep Patterns */}
          {deepPatterns.length > 0 && (
            <Card>
              <SectionTitle>Deep Patterns</SectionTitle>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {deepPatterns.map((p, i) => (
                  <div
                    key={i}
                    style={{
                      padding: 14,
                      background: C.surfaceHover,
                      borderRadius: 10,
                      border: `1px solid ${C.border}`,
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 8,
                        marginBottom: 6,
                      }}
                    >
                      <span style={{ fontSize: 18 }}>{p.icon || '🔬'}</span>
                      <span style={{ fontWeight: 600, fontSize: 14, color: C.text }}>
                        {p.title}
                      </span>
                    </div>
                    <p style={{ fontSize: 12, color: C.textMuted, margin: 0, lineHeight: 1.5 }}>
                      {p.body}
                    </p>
                    {p.formula && (
                      <div
                        style={{
                          marginTop: 8,
                          fontSize: 11,
                          fontFamily: "'JetBrains Mono', monospace",
                          color: C.accent,
                          background: C.accentDim,
                          padding: '3px 8px',
                          borderRadius: 4,
                          display: 'inline-block',
                        }}
                      >
                        {p.formula}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </Card>
          )}

          {/* Strengths */}
          {strengths.length > 0 && (
            <Card>
              <SectionTitle>Strengths</SectionTitle>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                {strengths.map((s, i) => (
                  <div
                    key={i}
                    style={{
                      padding: 14,
                      background: C.surfaceHover,
                      borderRadius: 10,
                      border: `1px solid ${C.border}`,
                      borderLeft: `3px solid ${s.color || C.up}`,
                    }}
                  >
                    <div style={{ fontWeight: 600, fontSize: 14, color: C.text, marginBottom: 4 }}>
                      {s.title}
                    </div>
                    <p style={{ fontSize: 12, color: C.textMuted, margin: 0, lineHeight: 1.5 }}>
                      {s.detail}
                    </p>
                    {s.signal && (
                      <div
                        style={{
                          marginTop: 6,
                          fontSize: 10,
                          color: s.color || C.accent,
                          fontWeight: 500,
                        }}
                      >
                        {s.signal}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </Card>
          )}
        </div>

        {/* Leverage Points */}
        {leveragePoints.length > 0 && (
          <Card>
            <SectionTitle>Leverage Points</SectionTitle>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {leveragePoints.map((lp) => (
                <div
                  key={lp.rank}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 14,
                    padding: 14,
                    background: C.surfaceHover,
                    borderRadius: 10,
                    border: `1px solid ${C.border}`,
                  }}
                >
                  <div
                    style={{
                      width: 32,
                      height: 32,
                      borderRadius: '50%',
                      background: lp.color || C.accent,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontWeight: 700,
                      fontSize: 14,
                      color: '#fff',
                      flexShrink: 0,
                    }}
                  >
                    {lp.rank}
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 600, fontSize: 14, color: C.text, marginBottom: 2 }}>
                      {lp.title}
                    </div>
                    <p style={{ fontSize: 12, color: C.textMuted, margin: 0 }}>
                      {lp.description}
                    </p>
                    {lp.signals.length > 0 && (
                      <div style={{ display: 'flex', gap: 6, marginTop: 6, flexWrap: 'wrap' }}>
                        {lp.signals.map((sig) => (
                          <span
                            key={sig}
                            style={{
                              fontSize: 10,
                              background: C.accentDim,
                              color: C.accent,
                              padding: '2px 6px',
                              borderRadius: 4,
                            }}
                          >
                            {sig}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                  <div style={{ textAlign: 'right', flexShrink: 0 }}>
                    <div style={{ fontSize: 11, color: C.textMuted }}>Impact</div>
                    <ImpactBar value={lp.impact} color={lp.color || C.accent} />
                    <div style={{ fontSize: 11, color: C.textMuted, marginTop: 4 }}>Feasibility</div>
                    <ImpactBar value={lp.feasibility} color={C.up} />
                  </div>
                </div>
              ))}
            </div>
          </Card>
        )}

        {/* Two-column: System Radar + Energy Curve */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))',
            gap: 14,
          }}
        >
          {/* System Radar */}
          {systemRadar.length > 0 && (
            <Card>
              <SectionTitle>System Radar</SectionTitle>
              <ResponsiveContainer width="100%" height={280}>
                <RadarChart data={systemRadar}>
                  <PolarGrid stroke={C.border} />
                  <PolarAngleAxis dataKey="system" tick={{ fill: C.textMuted, fontSize: 11 }} />
                  <PolarRadiusAxis tick={false} axisLine={false} domain={[0, 100]} />
                  <Radar
                    name="Healthy"
                    dataKey="healthy"
                    stroke={C.up}
                    fill={C.up}
                    fillOpacity={0.1}
                    strokeDasharray="4 3"
                  />
                  <Radar
                    name="Current"
                    dataKey="current"
                    stroke={C.accent}
                    fill={C.accent}
                    fillOpacity={0.2}
                  />
                  <Tooltip
                    contentStyle={{
                      background: C.surface,
                      border: `1px solid ${C.border}`,
                      borderRadius: 8,
                      fontSize: 12,
                    }}
                  />
                </RadarChart>
              </ResponsiveContainer>
            </Card>
          )}

          {/* Energy Curve */}
          {energyCurve.length > 0 && (
            <Card>
              <SectionTitle>Energy Curve</SectionTitle>
              <ResponsiveContainer width="100%" height={280}>
                <AreaChart data={energyCurve}>
                  <XAxis
                    dataKey="hour"
                    tickFormatter={(h: number) => `${h}:00`}
                    tick={{ fill: C.textMuted, fontSize: 11 }}
                    stroke={C.border}
                  />
                  <YAxis
                    domain={[0, 100]}
                    tick={{ fill: C.textMuted, fontSize: 11 }}
                    stroke={C.border}
                  />
                  <Tooltip
                    contentStyle={{
                      background: C.surface,
                      border: `1px solid ${C.border}`,
                      borderRadius: 8,
                      fontSize: 12,
                    }}
                    labelFormatter={(h) => `${h}:00`}
                  />
                  <Area
                    type="monotone"
                    dataKey="healthy"
                    stroke={C.up}
                    fill={C.upDim}
                    strokeDasharray="4 3"
                    name="Healthy"
                  />
                  <Area
                    type="monotone"
                    dataKey="current"
                    stroke={C.accent}
                    fill={C.accentDim}
                    name="Current"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}

// ═══ HELPER COMPONENTS ═══

function InsightCard({ insight, index }: { insight: PersonalSphereInsight; index: number }) {
  const color = insight.color || GRADIENT_COLORS[index % GRADIENT_COLORS.length];
  const dimColor = insight.colorDim || `${color}20`;
  const glowColor = insight.colorGlow || `${color}40`;

  return (
    <div
      style={{
        background: C.surface,
        border: `1px solid ${C.border}`,
        borderRadius: 12,
        padding: 18,
        borderTop: `2px solid ${color}`,
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      {/* Glow effect */}
      <div
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: 60,
          background: `linear-gradient(180deg, ${glowColor} 0%, transparent 100%)`,
          pointerEvents: 'none',
        }}
      />
      <div style={{ position: 'relative' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: 8 }}>
          <h3 style={{ fontSize: 15, fontWeight: 600, color: C.text, margin: 0 }}>
            {insight.title}
          </h3>
          {insight.domain && (
            <span
              style={{
                fontSize: 10,
                color,
                background: dimColor,
                padding: '2px 8px',
                borderRadius: 10,
                fontWeight: 500,
              }}
            >
              {insight.domain}
            </span>
          )}
        </div>
        <p style={{ fontSize: 13, color: C.textMuted, margin: '0 0 10px', lineHeight: 1.6 }}>
          {insight.body}
        </p>
        {insight.why && (
          <div style={{ fontSize: 12, color: C.textMuted, fontStyle: 'italic', marginBottom: 8 }}>
            Why: {insight.why}
          </div>
        )}
        {insight.formulas.length > 0 && (
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
            {insight.formulas.map((f, i) => (
              <span
                key={i}
                style={{
                  fontSize: 10,
                  fontFamily: "'JetBrains Mono', monospace",
                  color,
                  background: dimColor,
                  padding: '2px 6px',
                  borderRadius: 4,
                }}
              >
                {f}
              </span>
            ))}
          </div>
        )}
        {insight.signals && Object.keys(insight.signals).length > 0 && (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {Object.entries(insight.signals).map(([key, val]) => (
              <div key={key} style={{ fontSize: 11, color: C.textMuted }}>
                <span style={{ color, fontWeight: 600 }}>{key}</span>
                <span style={{ marginLeft: 4 }}>{val}%</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function ImpactBar({ value, color }: { value: number; color: string }) {
  return (
    <div
      style={{
        width: 60,
        height: 6,
        background: C.surfaceHover,
        borderRadius: 3,
        overflow: 'hidden',
      }}
    >
      <div
        style={{
          width: `${Math.max(0, Math.min(100, value))}%`,
          height: '100%',
          background: color,
          borderRadius: 3,
        }}
      />
    </div>
  );
}
