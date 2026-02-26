import { useState, useEffect } from "react";
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, RadarChart, PolarGrid, PolarAngleAxis, PolarRadiusAxis, Radar, BarChart, Bar, Cell, AreaChart, Area, ScatterChart, Scatter, ZAxis, CartesianGrid } from "recharts";
import { LoadingSpinner } from "@/components/LoadingSpinner";
import { usePersonStore } from "@/stores/personStore";
import { biosphereApi } from "@/api/biosphere";
import type { BioSphereResponse } from "@/types";

// ═══ COLOR SYSTEM ═══
const C = {
  bg: "#0a0b0f",
  surface: "#12131a",
  surfaceHover: "#1a1b24",
  border: "#1e2030",
  borderActive: "#2a3050",
  text: "#e8e6e3",
  textMuted: "#6b7084",
  textDim: "#3d4155",
  accent: "#5b8af5",
  accentDim: "#2a3f7a",
  up: "#4ade80",
  upDim: "#1a4a2e",
  down: "#f87171",
  downDim: "#4a1a1a",
  warn: "#fbbf24",
  warnDim: "#4a3a1a",
  purple: "#a78bfa",
  purpleDim: "#2a1f4a",
  cyan: "#22d3ee",
  cyanDim: "#0a3a3f",
  orange: "#fb923c",
  pink: "#f472b6",
};

// ═══ SIGNAL COLORS ═══
const SIGNAL_COLORS: Record<string, string> = {
  DA: C.accent, "5HT": C.purple, NE: C.cyan, GABA: C.warn,
  GLU: C.pink, CORT: C.down, BDNF: C.up, OXT: C.up,
  IL6: C.orange, ANA: C.purple, ENK: C.cyan, SUB_P: C.orange,
  ENDO: C.up, DYN: C.warn,
};
const PALETTE = [C.accent, C.purple, C.cyan, C.warn, C.orange, C.up, C.down, C.pink];
function getSignalColor(signal: string, index: number): string {
  return SIGNAL_COLORS[signal] ?? PALETTE[index % PALETTE.length];
}

// ═══ COMPONENTS ═══
const StatusDot = ({ status }: { status: string }) => {
  const colors: Record<string, string> = { critical: C.down, high: C.orange, moderate: C.warn, low: C.accent, intact: C.up, broken: C.down, degraded: C.orange, active: C.down, latched: C.down, inactive: C.textDim, exceeded: C.down, normal: C.up, unstable: C.warn, blunted: C.orange, accumulating: C.warn, emerging: C.warn, none: C.textDim };
  return <span style={{ display: "inline-block", width: 8, height: 8, borderRadius: "50%", background: colors[status] || C.textMuted, marginRight: 6, boxShadow: `0 0 6px ${colors[status] || C.textMuted}44` }} />;
};
const Card = ({ title, subtitle, span = 1, children, accent }: { title: string; subtitle?: string; span?: number; children: React.ReactNode; accent?: string }) => (
  <div style={{
    gridColumn: `span ${span}`,
    background: C.surface,
    border: `1px solid ${C.border}`,
    borderRadius: 8,
    padding: "16px 18px",
    position: "relative",
    overflow: "hidden",
  }}>
    {accent && <div style={{ position: "absolute", top: 0, left: 0, right: 0, height: 2, background: accent }} />}
    <div style={{ marginBottom: 12 }}>
      <div style={{ fontSize: 13, fontWeight: 600, color: C.text, letterSpacing: "0.02em" }}>{title}</div>
      {subtitle && <div style={{ fontSize: 10, color: C.textMuted, marginTop: 2 }}>{subtitle}</div>}
    </div>
    {children}
  </div>
);
const StateChip = ({ state, trend }: { state: string; trend: string }) => {
  const stateColors: Record<string, string> = { "↑↑": C.down, "↑": C.warn, "≈": C.up, "↓": C.orange, "↓↓": C.down };
  const trendIcons: Record<string, string> = { increasing: "▲", declining: "▼", stable: "●" };
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 4,
      padding: "2px 8px", borderRadius: 4, fontSize: 11, fontWeight: 600, fontFamily: "monospace",
      background: `${stateColors[state] || C.textMuted}18`,
      color: stateColors[state] || C.textMuted,
      border: `1px solid ${stateColors[state] || C.textMuted}30`,
    }}>
      {state} <span style={{ fontSize: 8 }}>{trendIcons[trend]}</span>
    </span>
  );
};
const HeatCell = ({ value }: { value: number }) => {
  const getColor = (v: number) => {
    if (v < 35) return C.down;
    if (v < 45) return C.orange;
    if (v < 55) return C.up;
    if (v < 65) return C.warn;
    return C.down;
  };
  return (
    <td style={{
      padding: "6px 10px", textAlign: "center", fontSize: 11, fontFamily: "monospace",
      background: `${getColor(value)}15`, color: getColor(value), fontWeight: 600,
      border: `1px solid ${C.border}`,
    }}>{value}</td>
  );
};
const EmptyState = ({ message }: { message: string }) => (
  <div style={{
    padding: 24, textAlign: "center", color: C.textDim, fontSize: 12,
    border: `1px dashed ${C.border}`, borderRadius: 6,
  }}>
    {message}
  </div>
);

const tabs = [
  "Overview", "Signals", "Pathways", "Gates & Lifecycles", "Loops & Failures", "Trajectories", "Cross-Analysis"
];

export default function BioSpherePage() {
  const { activePerson } = usePersonStore();
  const [data, setData] = useState<BioSphereResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState(0);
  const [selectedSignal, setSelectedSignal] = useState<string | null>(null);

  useEffect(() => {
    if (!activePerson) return;
    setLoading(true);
    setError(null);
    biosphereApi.get(activePerson)
      .then(setData)
      .catch((err) => setError(err?.response?.data?.error ?? "Failed to load BioSphere"))
      .finally(() => setLoading(false));
  }, [activePerson]);

  if (!activePerson) {
    return (
      <div style={{ padding: 32, color: C.textMuted, textAlign: "center", fontFamily: "'IBM Plex Sans', sans-serif" }}>
        Select a person to view BioSphere analysis.
      </div>
    );
  }
  if (loading) {
    return (
      <div style={{ padding: 32 }}>
        <LoadingSpinner text="Loading BioSphere analysis..." />
      </div>
    );
  }
  if (error) {
    return (
      <div style={{ padding: 32, color: C.down, textAlign: "center", fontFamily: "'IBM Plex Sans', sans-serif" }}>
        {error}
      </div>
    );
  }
  if (!data) return null;

  // ═══ DERIVED DATA ═══
  const criticalLoopCount = data.loops.filter(l => l.severity === "critical").length;
  const warningLoopCount = data.loops.filter(l => ["high", "moderate"].includes(l.severity)).length;

  const trajectorySignals = data.trajectory.length > 0
    ? Object.keys(data.trajectory[0]).filter(k => k !== "phase" && k !== "label").slice(0, 6)
    : [];

  const heatmapSignals = data.regionHeatmap.length > 0
    ? Object.keys(data.regionHeatmap[0]).filter(k => k !== "region")
    : [];

  const lastAnalysisText = (() => {
    try {
      const d = new Date(data.lastAnalysis);
      const diff = Date.now() - d.getTime();
      const hours = Math.floor(diff / 3600000);
      if (hours < 1) return "just now";
      if (hours < 24) return `${hours}h ago`;
      return `${Math.floor(hours / 24)}d ago`;
    } catch {
      return data.lastAnalysis;
    }
  })();

  // Cross-analysis: derive signal pairs with divergent values
  const crossAnalysis = data.signalProfile.length >= 3
    ? data.signalProfile.flatMap((a, i) =>
        data.signalProfile.slice(i + 1)
          .filter(b => Math.abs(a.value - b.value) > 15)
          .map(b => ({
            x: a.value, y: b.value,
            z: Math.abs(a.value - b.value) * 5,
            label: `${a.signal} vs ${b.signal}`,
            category: a.value > 50 ? "high" : "low",
          }))
      ).slice(0, 12)
    : [];

  return (
    <div style={{ background: C.bg, color: C.text, minHeight: "100vh", fontFamily: "'IBM Plex Sans', -apple-system, sans-serif" }}>
      <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@300;400;500;600;700&family=IBM+Plex+Mono:wght@400;500&display=swap" rel="stylesheet" />
      {/* Header */}
      <div style={{ borderBottom: `1px solid ${C.border}`, padding: "14px 24px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <div style={{ fontSize: 16, fontWeight: 700, letterSpacing: "-0.02em" }}>
            <span style={{ color: C.accent }}>BioChain</span> Analysis Dashboard
          </div>
          <div style={{ fontSize: 11, color: C.textMuted, marginTop: 2 }}>
            Person: <span style={{ color: C.text }}>{data.person}</span> · Last analysis: {lastAnalysisText}
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          {criticalLoopCount > 0 && (
            <span style={{ fontSize: 10, color: C.textMuted, padding: "4px 10px", background: `${C.down}15`, border: `1px solid ${C.down}30`, borderRadius: 4 }}>
              <StatusDot status="critical" /> {criticalLoopCount} Critical Loop{criticalLoopCount > 1 ? "s" : ""}
            </span>
          )}
          {warningLoopCount > 0 && (
            <span style={{ fontSize: 10, color: C.textMuted, padding: "4px 10px", background: `${C.warn}15`, border: `1px solid ${C.warn}30`, borderRadius: 4 }}>
              {warningLoopCount} Warning{warningLoopCount > 1 ? "s" : ""}
            </span>
          )}
          {data.trajectory.length > 0 && (
            <span style={{ fontSize: 10, color: C.textMuted, padding: "4px 10px", background: `${C.accent}15`, border: `1px solid ${C.accent}30`, borderRadius: 4 }}>
              {data.trajectory.length} trajectory phases
            </span>
          )}
        </div>
      </div>
      {/* Tabs */}
      <div style={{ borderBottom: `1px solid ${C.border}`, padding: "0 24px", display: "flex", gap: 0, overflowX: "auto" }}>
        {tabs.map((t, i) => (
          <button key={t} onClick={() => setActiveTab(i)} style={{
            background: "none", border: "none", borderBottom: `2px solid ${activeTab === i ? C.accent : "transparent"}`,
            color: activeTab === i ? C.text : C.textMuted, fontSize: 12, fontWeight: 500,
            padding: "10px 16px", cursor: "pointer", whiteSpace: "nowrap", transition: "all 0.15s",
            fontFamily: "inherit",
          }}>{t}</button>
        ))}
      </div>
      {/* Content */}
      <div style={{ padding: 20 }}>
        {/* ═══ OVERVIEW TAB ═══ */}
        {activeTab === 0 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
            {/* 1 · Radar */}
            <Card title="BASELINE CHEMICAL PROFILE" subtitle="Behavioral dimension radar from dimension scores" accent={C.accent}>
              {data.radar.length > 0 ? (
                <ResponsiveContainer width="100%" height={200}>
                  <RadarChart data={data.radar}>
                    <PolarGrid stroke={C.border} />
                    <PolarAngleAxis dataKey="dim" tick={{ fill: C.textMuted, fontSize: 9 }} />
                    <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
                    <Radar dataKey="value" stroke={C.accent} fill={C.accent} fillOpacity={0.15} strokeWidth={2} />
                  </RadarChart>
                </ResponsiveContainer>
              ) : <EmptyState message="No dimension data available yet. Run an analysis first." />}
            </Card>
            {/* 2 · Trajectory */}
            <Card title="TRAJECTORY PROGRESSION" subtitle="Signal levels over time from observation timeline" accent={C.warn}>
              {data.trajectory.length > 0 && trajectorySignals.length > 0 ? (
                <>
                  <ResponsiveContainer width="100%" height={200}>
                    <LineChart data={data.trajectory}>
                      <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                      <XAxis dataKey="label" tick={{ fill: C.textMuted, fontSize: 10 }} />
                      <YAxis domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                      <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                      {trajectorySignals.map((sig, i) => (
                        <Line key={sig} type="monotone" dataKey={sig} stroke={getSignalColor(sig, i)} strokeWidth={2} dot={{ r: 3 }} />
                      ))}
                    </LineChart>
                  </ResponsiveContainer>
                  <div style={{ display: "flex", gap: 12, marginTop: 6, justifyContent: "center" }}>
                    {trajectorySignals.map((sig, i) => (
                      <span key={sig} style={{ fontSize: 9, color: getSignalColor(sig, i), display: "flex", alignItems: "center", gap: 3 }}>
                        <span style={{ width: 8, height: 2, background: getSignalColor(sig, i), display: "inline-block" }} /> {sig}
                      </span>
                    ))}
                  </div>
                </>
              ) : <EmptyState message="No trajectory data available yet." />}
            </Card>
            {/* 3 · Active Loops */}
            <Card title="ACTIVE LOOPS" subtitle="Feedback loops detected in analysis" accent={C.down}>
              {data.loops.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  {data.loops.map(l => (
                    <div key={l.name} style={{
                      padding: "8px 10px", borderRadius: 6, fontSize: 11,
                      background: `${l.status === "broken" || l.status === "active" ? C.down : l.status === "degraded" ? C.orange : l.status === "latched" ? C.down : C.up}08`,
                      border: `1px solid ${l.status === "broken" || l.status === "active" ? C.down : l.status === "degraded" ? C.orange : C.up}20`,
                    }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                        <span><StatusDot status={l.status} /><strong>{l.name}</strong></span>
                        <span style={{ fontSize: 9, color: C.textMuted, textTransform: "uppercase" }}>{l.type} · {l.status}</span>
                      </div>
                      <div style={{ fontSize: 9, color: C.textMuted, fontFamily: "'IBM Plex Mono'", marginTop: 3 }}>{l.formula}</div>
                    </div>
                  ))}
                </div>
              ) : <EmptyState message="No active loops detected." />}
            </Card>
            {/* 4 · Signal States Table */}
            <Card title="SIGNAL STATES TABLE" subtitle="Top signals by observation frequency" span={2} accent={C.cyan}>
              {data.signalProfile.length > 0 ? (
                <div style={{ overflowX: "auto" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 11 }}>
                    <thead>
                      <tr style={{ borderBottom: `1px solid ${C.border}` }}>
                        {["Signal", "Code", "Value", "State", "Region", "Trend", "Status"].map(h => (
                          <th key={h} style={{ padding: "6px 10px", textAlign: "left", color: C.textMuted, fontSize: 10, fontWeight: 500 }}>{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {data.signalProfile.map(s => (
                        <tr key={s.signal} style={{ borderBottom: `1px solid ${C.border}`, cursor: "pointer" }}
                          onClick={() => setSelectedSignal(s.signal === selectedSignal ? null : s.signal)}>
                          <td style={{ padding: "6px 10px", fontWeight: 500 }}>{s.label}</td>
                          <td style={{ padding: "6px 10px", fontFamily: "'IBM Plex Mono'", color: C.accent, fontSize: 10 }}>{s.signal}</td>
                          <td style={{ padding: "6px 10px", fontFamily: "'IBM Plex Mono'", fontSize: 10 }}>{s.value}%</td>
                          <td style={{ padding: "6px 10px" }}><StateChip state={s.state} trend={s.trend} /></td>
                          <td style={{ padding: "6px 10px", fontFamily: "'IBM Plex Mono'", fontSize: 10, color: C.textMuted }}>{s.region || "—"}</td>
                          <td style={{ padding: "6px 10px", color: s.trend === "declining" ? C.down : s.trend === "increasing" ? C.warn : C.up, fontSize: 10 }}>
                            {s.trend}
                          </td>
                          <td style={{ padding: "6px 10px", fontSize: 10, color: s.value > 70 || s.value < 35 ? C.orange : C.textDim }}>
                            {s.value > 75 ? "excess" : s.value < 35 ? "depletion" : "—"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : <EmptyState message="No signal data available." />}
            </Card>
            {/* 5 · Failure Mode Severity */}
            <Card title="FAILURE MODE SEVERITY" subtitle="Failure modes from active loop analysis" accent={C.orange}>
              {data.failureModes.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: 5, marginTop: 4 }}>
                  {data.failureModes.map(f => (
                    <div key={f.name} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <span style={{ width: 70, fontSize: 9, color: C.textMuted, textAlign: "right", whiteSpace: "pre-line", lineHeight: 1.2 }}>{f.name}</span>
                      <div style={{ flex: 1, height: 14, background: C.bg, borderRadius: 3, overflow: "hidden" }}>
                        <div style={{ width: `${Math.min(f.size * 20, 100)}%`, height: "100%", background: f.color, borderRadius: 3, opacity: 0.7 }} />
                      </div>
                      <span style={{ fontSize: 9, color: f.color, width: 50, textAlign: "right" }}>{f.severity}</span>
                    </div>
                  ))}
                </div>
              ) : <EmptyState message="No failure modes detected." />}
            </Card>
            {/* 6 · Region Heatmap */}
            <Card title="REGION HEATMAP" subtitle="Signal intensity by brain region" span={2} accent={C.purple}>
              {data.regionHeatmap.length > 0 && heatmapSignals.length > 0 ? (
                <table style={{ width: "100%", borderCollapse: "collapse" }}>
                  <thead>
                    <tr>
                      <th style={{ padding: 6, fontSize: 10, color: C.textMuted, textAlign: "left" }}>Region</th>
                      {heatmapSignals.map(h => (
                        <th key={h} style={{ padding: 6, fontSize: 10, color: C.textMuted, textAlign: "center" }}>{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {data.regionHeatmap.map((r, ri) => (
                      <tr key={ri}>
                        <td style={{ padding: 6, fontSize: 11, fontFamily: "'IBM Plex Mono'", color: C.accent }}>{r.region}</td>
                        {heatmapSignals.map(sig => (
                          <HeatCell key={sig} value={typeof r[sig] === "number" ? r[sig] as number : 0} />
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : <EmptyState message="No region-tagged observations available yet." />}
            </Card>
            {/* 7 · Cascades */}
            <Card title="CASCADE IMPACT" subtitle="Signal interaction chains" accent={C.down}>
              {data.cascades.length > 0 ? (
                <div>
                  {data.cascades.map(c => (
                    <div key={c.source} style={{ marginBottom: 12 }}>
                      <div style={{ fontSize: 11, fontWeight: 600, color: C.down, marginBottom: 6 }}>{c.source}</div>
                      {c.targets.map(t => (
                        <div key={t.name} style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
                          <span style={{ width: 60, fontSize: 11, fontFamily: "'IBM Plex Mono'", color: C.textMuted }}>{t.name}</span>
                          <div style={{ flex: 1, height: 10, background: C.bg, borderRadius: 3, overflow: "hidden" }}>
                            <div style={{ width: `${Math.min(t.impact, 100)}%`, height: "100%", background: `linear-gradient(90deg, ${C.down}, ${C.orange})`, borderRadius: 3, opacity: 0.6 }} />
                          </div>
                          <span style={{ fontSize: 10, color: C.textMuted, width: 35, textAlign: "right" }}>{t.impact}%</span>
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              ) : <EmptyState message="No cascade data available yet." />}
            </Card>
          </div>
        )}
        {/* ═══ SIGNALS TAB ═══ */}
        {activeTab === 1 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="SIGNAL INTENSITY BAR CHART" subtitle="Signal observation frequency distribution" span={2} accent={C.accent}>
              {data.signalProfile.length > 0 ? (
                <>
                  <ResponsiveContainer width="100%" height={250}>
                    <BarChart data={data.signalProfile} layout="vertical">
                      <CartesianGrid stroke={C.border} strokeDasharray="3 3" horizontal={false} />
                      <XAxis type="number" domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                      <YAxis type="category" dataKey="label" width={100} tick={{ fill: C.textMuted, fontSize: 11 }} />
                      <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                      <Bar dataKey="value" radius={[0, 4, 4, 0]}>
                        {data.signalProfile.map((entry) => (
                          <Cell key={entry.signal} fill={entry.value > 70 ? C.down : entry.value < 35 ? C.orange : entry.value < 45 ? C.warn : C.up} fillOpacity={0.7} />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                  <div style={{ textAlign: "center", fontSize: 9, color: C.textMuted, marginTop: 4 }}>
                    <span style={{ color: C.up }}>45-70 healthy</span>{" · "}
                    <span style={{ color: C.warn }}>35-45 low</span>{" · "}
                    <span style={{ color: C.orange }}>&lt;35 depleted</span>{" · "}
                    <span style={{ color: C.down }}>&gt;70 excess</span>
                  </div>
                </>
              ) : <EmptyState message="No signal data available." />}
            </Card>
            <Card title="CASCADE IMPACT" subtitle="Signal interaction downstream effects" accent={C.down}>
              {data.cascades.length > 0 ? (
                <div>
                  {data.cascades.map(c => (
                    <div key={c.source}>
                      <div style={{ fontSize: 11, fontWeight: 600, color: C.down, marginBottom: 6 }}>{c.source}</div>
                      {c.targets.map(t => (
                        <div key={t.name} style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
                          <span style={{ width: 60, fontSize: 11, fontFamily: "'IBM Plex Mono'", color: C.textMuted }}>{t.name}</span>
                          <div style={{ flex: 1, height: 10, background: C.bg, borderRadius: 3, overflow: "hidden" }}>
                            <div style={{ width: `${Math.min(t.impact, 100)}%`, height: "100%", background: `linear-gradient(90deg, ${C.down}, ${C.orange})`, borderRadius: 3, opacity: 0.6 }} />
                          </div>
                          <span style={{ fontSize: 10, color: C.textMuted, width: 35, textAlign: "right" }}>{t.impact}%</span>
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              ) : <EmptyState message="No cascade data available yet. Signal interactions will appear after sufficient analysis." />}
            </Card>
            <Card title="LIFECYCLE VULNERABILITY" subtitle="Per-stage healthy vs current comparison" accent={C.cyan}>
              {data.lifecycle.length > 0 ? (
                <>
                  <ResponsiveContainer width="100%" height={200}>
                    <BarChart data={data.lifecycle}>
                      <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                      <XAxis dataKey="stage" tick={{ fill: C.textMuted, fontSize: 9 }} />
                      <YAxis domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                      <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                      <Bar dataKey="healthy" fill={C.up} fillOpacity={0.3} radius={[2, 2, 0, 0]} />
                      <Bar dataKey="current" radius={[2, 2, 0, 0]}>
                        {data.lifecycle.map((entry) => (
                          <Cell key={entry.stage} fill={entry.vulnerable ? C.orange : C.accent} fillOpacity={0.7} />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                  <div style={{ textAlign: "center", fontSize: 9, color: C.textMuted }}>
                    <span style={{ color: `${C.up}88` }}>Healthy</span>{" · "}
                    <span style={{ color: C.accent }}>Current (ok)</span>{" · "}
                    <span style={{ color: C.orange }}>Current (vulnerable)</span>
                  </div>
                </>
              ) : <EmptyState message="No lifecycle data available yet." />}
            </Card>
          </div>
        )}
        {/* ═══ PATHWAYS TAB ═══ */}
        {activeTab === 2 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="PATHWAY STATUS" subtitle="Loop and cascade pathways from active analysis data" span={2} accent={C.purple}>
              {data.loops.length > 0 ? (
                <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 11, lineHeight: 2, padding: 12, background: C.bg, borderRadius: 6 }}>
                  {data.loops.map(l => {
                    const statusColor = l.status === "broken" || l.status === "active" ? C.down
                      : l.status === "degraded" ? C.orange
                      : l.status === "intact" ? C.up : C.textDim;
                    return (
                      <div key={l.name} style={{ marginBottom: 16 }}>
                        <div>
                          <span style={{ color: C.warn }}>PATHWAY</span>{" "}
                          <span style={{ color: C.accent }}>{l.name}</span>{" "}
                          <span style={{ color: C.textMuted }}>({l.type})</span>
                        </div>
                        <div style={{ marginLeft: 16 }}>
                          <span style={{ color: statusColor }}>{l.status === "broken" || l.status === "active" ? "✗" : l.status === "degraded" ? "◐" : "●"}</span>{" "}
                          <span style={{ color: C.textMuted }}>{l.formula}</span>
                        </div>
                        <div style={{ marginLeft: 16 }}>
                          <span style={{ color: statusColor, textTransform: "uppercase", fontSize: 10 }}>
                            {l.status === "broken" ? "BROKEN" : l.status}{l.severity === "critical" ? " — CRITICAL" : ""}
                          </span>
                        </div>
                        {l.signals.length > 0 && (
                          <div style={{ marginLeft: 16, fontSize: 9, color: C.textMuted }}>
                            Signals: {l.signals.join(", ")}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              ) : <EmptyState message="No pathway data available yet. Run an analysis to detect biochemical pathways." />}
            </Card>
          </div>
        )}
        {/* ═══ GATES & LIFECYCLES TAB ═══ */}
        {activeTab === 3 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="GATE STATUS" subtitle="Gate instances from signal interaction analysis" span={2} accent={C.cyan}>
              {data.gates.length > 0 ? (
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8 }}>
                  {data.gates.map(g => (
                    <div key={g.instance} style={{
                      padding: "10px 12px", borderRadius: 6, background: C.bg,
                      border: `1px solid ${g.status === "normal" ? C.up : g.status === "latched" || g.status === "exceeded" ? C.down : C.warn}25`,
                    }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 4 }}>
                        <span style={{ fontSize: 13, fontWeight: 700, fontFamily: "'IBM Plex Mono'" }}>{g.gate}</span>
                        <StatusDot status={g.status} />
                      </div>
                      <div style={{ fontSize: 11, fontWeight: 500, marginBottom: 4 }}>{g.instance}</div>
                      <div style={{ fontSize: 9, fontFamily: "'IBM Plex Mono'", color: C.textMuted }}>{g.formula}</div>
                      <div style={{ fontSize: 9, marginTop: 4, color: g.status === "normal" ? C.up : g.status === "latched" || g.status === "exceeded" ? C.down : C.warn, textTransform: "uppercase" }}>
                        {g.status}
                      </div>
                    </div>
                  ))}
                </div>
              ) : <EmptyState message="No gate data available yet. Gate analysis requires signal interaction data." />}
            </Card>
            <Card title="LIFECYCLE STAGES" subtitle="Signal lifecycle stage comparison" span={2} accent={C.purple}>
              {data.lifecycle.length > 0 ? (
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                  {data.lifecycle.map(l => (
                    <div key={l.stage} style={{
                      padding: "10px 12px", borderRadius: 6, background: C.bg,
                      border: `1px solid ${l.vulnerable ? C.orange : C.up}25`,
                    }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 4 }}>
                        <span style={{ fontSize: 12, fontWeight: 600 }}>{l.stage}</span>
                        <span style={{ fontSize: 10, color: l.vulnerable ? C.orange : C.up, textTransform: "uppercase" }}>
                          {l.vulnerable ? "vulnerable" : "ok"}
                        </span>
                      </div>
                      <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                        <div style={{ flex: 1 }}>
                          <div style={{ fontSize: 9, color: C.textMuted, marginBottom: 2 }}>Healthy: {l.healthy}</div>
                          <div style={{ height: 6, background: C.bg, borderRadius: 3, overflow: "hidden", border: `1px solid ${C.border}` }}>
                            <div style={{ width: `${l.healthy}%`, height: "100%", background: C.up, opacity: 0.4, borderRadius: 3 }} />
                          </div>
                        </div>
                        <div style={{ flex: 1 }}>
                          <div style={{ fontSize: 9, color: C.textMuted, marginBottom: 2 }}>Current: {l.current}</div>
                          <div style={{ height: 6, background: C.bg, borderRadius: 3, overflow: "hidden", border: `1px solid ${C.border}` }}>
                            <div style={{ width: `${l.current}%`, height: "100%", background: l.vulnerable ? C.orange : C.accent, opacity: 0.7, borderRadius: 3 }} />
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : <EmptyState message="No lifecycle data available yet." />}
            </Card>
          </div>
        )}
        {/* ═══ LOOPS & FAILURES TAB ═══ */}
        {activeTab === 4 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="FEEDBACK LOOP MAP" subtitle="All detected feedback loops and their current status" span={2} accent={C.down}>
              {data.loops.length > 0 ? (
                <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 11, lineHeight: 1.8, padding: 16, background: C.bg, borderRadius: 6 }}>
                  {data.loops.map(l => {
                    const isVicious = l.status === "broken" || l.status === "active" || l.status === "degraded";
                    const isDormant = l.status === "inactive";
                    const loopColor = isVicious ? C.down : isDormant ? C.textDim : C.up;
                    const loopLabel = isVicious ? "VICIOUS" : isDormant ? "DORMANT" : l.status === "intact" ? "VIRTUOUS" : l.status.toUpperCase();
                    return (
                      <div key={l.name} style={{ marginBottom: 16 }}>
                        <div>
                          <span style={{ color: loopColor }}>{l.type === "PFB" ? "⟳⁺" : "⟳⁻"} {loopLabel}</span>{" "}
                          <span style={{ color: C.textMuted }}>— {l.name} ({l.severity})</span>
                        </div>
                        <div style={{ marginLeft: 8, borderLeft: `2px solid ${loopColor}40`, paddingLeft: 12 }}>
                          {l.formula}<br />
                          <span style={{ color: loopColor }}>Status: {l.status}</span>
                          {l.signals.length > 0 && (
                            <span style={{ color: C.textMuted }}> · Signals: {l.signals.join(", ")}</span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : <EmptyState message="No feedback loops detected. Loops are identified through biochemical analysis." />}
            </Card>
          </div>
        )}
        {/* ═══ TRAJECTORIES TAB ═══ */}
        {activeTab === 5 && (
          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14 }}>
            <Card title="SIGNAL TRAJECTORY" subtitle="Signal levels over time from observation timeline" accent={C.warn}>
              {data.trajectory.length > 0 && trajectorySignals.length > 0 ? (
                <>
                  <ResponsiveContainer width="100%" height={260}>
                    <AreaChart data={data.trajectory}>
                      <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                      <XAxis dataKey="label" tick={{ fill: C.textMuted, fontSize: 11 }} />
                      <YAxis domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                      <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                      {trajectorySignals.map((sig, i) => (
                        <Area key={sig} type="monotone" dataKey={sig} stroke={getSignalColor(sig, i)} fill={getSignalColor(sig, i)} fillOpacity={0.08 + (i === 0 ? 0.02 : 0)} strokeWidth={2} />
                      ))}
                    </AreaChart>
                  </ResponsiveContainer>
                  <div style={{ display: "flex", gap: 12, justifyContent: "center", marginTop: 6 }}>
                    {trajectorySignals.map((sig, i) => (
                      <span key={sig} style={{ fontSize: 9, color: getSignalColor(sig, i) }}>● {sig}</span>
                    ))}
                  </div>
                </>
              ) : <EmptyState message="No trajectory data available yet." />}
            </Card>
            <Card title="PHASE DETAIL" subtitle="Trajectory phase breakdown" accent={C.warn}>
              {data.trajectory.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  {data.trajectory.map((phase, i) => {
                    const isLast = i === data.trajectory.length - 1;
                    const color = isLast ? C.down : i > data.trajectory.length * 0.7 ? C.orange : i > data.trajectory.length * 0.4 ? C.warn : C.up;
                    const signalSummary = trajectorySignals
                      .map(sig => `${sig}:${typeof phase[sig] === "number" ? Math.round(phase[sig] as number) : "?"}`)
                      .join(" ");
                    return (
                      <div key={i} style={{
                        padding: "8px 10px", borderRadius: 4, background: `${color}08`,
                        border: `1px solid ${color}20`, fontSize: 10,
                      }}>
                        <div style={{ fontWeight: 600, color, marginBottom: 2 }}>
                          {isLast ? "◄" : "✓"} {phase.phase as string || phase.label as string}
                        </div>
                        <div style={{ fontFamily: "'IBM Plex Mono'", color: C.textMuted, fontSize: 9 }}>
                          {signalSummary}
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : <EmptyState message="No phase data available." />}
            </Card>
          </div>
        )}
        {/* ═══ CROSS-ANALYSIS TAB ═══ */}
        {activeTab === 6 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="CROSS-SIGNAL CORRELATION" subtitle="Signal pairs with divergent levels" span={2} accent={C.pink}>
              {crossAnalysis.length > 0 ? (
                <>
                  <ResponsiveContainer width="100%" height={280}>
                    <ScatterChart>
                      <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                      <XAxis type="number" dataKey="x" name="Source Signal" domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }}
                        label={{ value: "Source signal level", position: "insideBottom", offset: -5, fill: C.textMuted, fontSize: 10 }} />
                      <YAxis type="number" dataKey="y" name="Target Signal" domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }}
                        label={{ value: "Target signal level", angle: -90, position: "insideLeft", fill: C.textMuted, fontSize: 10 }} />
                      <ZAxis type="number" dataKey="z" range={[60, 300]} />
                      <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }}
                        formatter={(v: number, name: string) => [v, name]}
                        labelFormatter={() => ""} />
                      <Scatter data={crossAnalysis} fill={C.pink} fillOpacity={0.6}>
                        {crossAnalysis.map((e, i) => (
                          <Cell key={i} fill={e.category === "high" ? C.down : C.accent} />
                        ))}
                      </Scatter>
                    </ScatterChart>
                  </ResponsiveContainer>
                  <div style={{ display: "flex", gap: 16, justifyContent: "center", marginTop: 6 }}>
                    <span style={{ fontSize: 9, color: C.down }}>● high divergence</span>
                    <span style={{ fontSize: 9, color: C.accent }}>● moderate divergence</span>
                  </div>
                </>
              ) : <EmptyState message="Not enough signal data for cross-analysis. Need at least 3 signals with divergent values." />}
            </Card>
            <Card title="AVAILABLE CROSS-ANALYSIS QUERIES" subtitle="Examples of queries this data enables" span={2} accent={C.accent}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, fontSize: 11 }}>
                {[
                  { q: "Which loops are driving the trajectory?", desc: "active_loop signals correlated with trajectory trends" },
                  { q: "What gate states changed between phases?", desc: "Gate status changes across trajectory timeline" },
                  { q: "Which pathways are most degraded?", desc: "Aggregate failure_mode per pathway from observations" },
                  { q: "What's the cascade if we fix a signal?", desc: "Walk signal interaction graph from a target signal" },
                  { q: "Find people with similar loop patterns", desc: "Compare active loop embeddings via cosine similarity" },
                  { q: "Which lifecycle stage is most vulnerable?", desc: "Cross-reference lifecycle stages with signal intensity" },
                  { q: "Compare two people's profiles", desc: "Side-by-side signal and dimension comparison" },
                  { q: "Predict next trajectory phase", desc: "Phase embedding similarity against known progressions" },
                ].map(item => (
                  <div key={item.q} style={{ padding: "10px 12px", background: C.bg, borderRadius: 6, border: `1px solid ${C.border}` }}>
                    <div style={{ fontWeight: 500, marginBottom: 4 }}>{item.q}</div>
                    <div style={{ fontSize: 9, color: C.textMuted }}>{item.desc}</div>
                  </div>
                ))}
              </div>
            </Card>
          </div>
        )}
      </div>
    </div>
  );
}
