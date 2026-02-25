import { useState } from "react";
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, RadarChart, PolarGrid, PolarAngleAxis, PolarRadiusAxis, Radar, BarChart, Bar, Cell, AreaChart, Area, ScatterChart, Scatter, ZAxis, CartesianGrid } from "recharts";
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
// ═══ MOCK DATA ═══
const baselineProfile = [
  { signal: "DA", label: "Dopamine", value: 42, state: "↓", region: "NAc", trend: "declining" },
  { signal: "5HT", label: "Serotonin", value: 35, state: "↓", region: "DRN", trend: "declining" },
  { signal: "NE", label: "Norepinephrine", value: 68, state: "↑", region: "LC", trend: "stable" },
  { signal: "GABA", label: "GABA", value: 38, state: "↓", region: "AMY", trend: "declining" },
  { signal: "GLU", label: "Glutamate", value: 72, state: "↑", region: "HPC", trend: "increasing" },
  { signal: "CORT", label: "Cortisol", value: 82, state: "↑↑", region: "HYP", trend: "increasing" },
  { signal: "BDNF", label: "BDNF", value: 30, state: "↓↓", region: "HPC", trend: "declining" },
  { signal: "OXT", label: "Oxytocin", value: 45, state: "≈", region: "PVN", trend: "stable" },
  { signal: "IL6", label: "IL-6", value: 71, state: "↑", region: "sys", trend: "increasing" },
  { signal: "ANA", label: "Anandamide", value: 32, state: "↓", region: "AMY", trend: "declining" },
];
const radarData = [
  { dim: "Reward", value: 35, fullMark: 100 },
  { dim: "Stress\nResilience", value: 28, fullMark: 100 },
  { dim: "Emotion\nRegulation", value: 40, fullMark: 100 },
  { dim: "Social\nBonding", value: 55, fullMark: 100 },
  { dim: "Cognitive\nFlexibility", value: 48, fullMark: 100 },
  { dim: "Energy", value: 32, fullMark: 100 },
  { dim: "Sensitivity", value: 72, fullMark: 100 },
  { dim: "Pain\nBuffer", value: 38, fullMark: 100 },
];
const trajectoryData = [
  { phase: "Baseline", DA: 55, HT: 52, CORT: 50, BDNF: 55, IL6: 30, label: "@t0" },
  { phase: "Acute", DA: 65, HT: 50, CORT: 68, BDNF: 52, IL6: 32, label: "@t1" },
  { phase: "Repeated", DA: 48, HT: 42, CORT: 75, BDNF: 40, IL6: 50, label: "@t2" },
  { phase: "Chronic", DA: 32, HT: 30, CORT: 85, BDNF: 25, IL6: 72, label: "@t3" },
  { phase: "Current", DA: 28, HT: 28, CORT: 82, BDNF: 22, IL6: 75, label: "@t4" },
];
const loopData = [
  { name: "HPA Feedback", type: "NFB", status: "broken", severity: "critical", formula: "CORT↑↑ → GR.resist → ⟳⁻.broken → CRH↑↑", signals: ["CORT", "CRH", "ACTH"] },
  { name: "Inflammatory Shunt", type: "PFB", status: "active", severity: "high", formula: "IL6↑ → IDO → TRP→KYN → 5HT↓ → BDNF↓", signals: ["IL6", "5HT", "BDNF"] },
  { name: "Rumination Cycle", type: "PFB", status: "degraded", severity: "moderate", formula: "5HT↓ → PFC⊣AMY↓ → rumination → CORT↑ → 5HT↓↓", signals: ["5HT", "CORT"] },
  { name: "DA-D2 Autoreceptor", type: "NFB", status: "intact", severity: "low", formula: "DA → D2.auto → ⊣DA.release (functioning)", signals: ["DA"] },
  { name: "Exercise-BDNF", type: "PFB", status: "inactive", severity: "none", formula: "exercise → βEND↑ → BDNF↑ → DA↑ → motivation (dormant)", signals: ["BDNF", "DA"] },
  { name: "OXT-DA Social", type: "PFB", status: "intact", severity: "low", formula: "OXT → DA↑@VTA → social_reward → OXT↑", signals: ["OXT", "DA"] },
];
const cascadeData = [
  { source: "CORT↑↑", targets: [
    { name: "5HT↓", impact: 0.85 },
    { name: "BDNF↓", impact: 0.78 },
    { name: "DA↓", impact: 0.62 },
    { name: "GABA↓", impact: 0.55 },
    { name: "IL6↑", impact: 0.70 },
    { name: "ANA↓", impact: 0.45 },
  ]},
];
const gateData = [
  { gate: "AND ⊼", instance: "NMDA activation", formula: "{⊼: GLU.bind, depolarization → Ca²⁺}", status: "normal" },
  { gate: "THRESHOLD ⊨", instance: "GR saturation", formula: "{⊨(CORT>high): GR.saturate → pathology}", status: "exceeded" },
  { gate: "XOR ⊕", instance: "Sleep-Wake", formula: "{⊕: ORX.active, MEL.active → state}", status: "unstable" },
  { gate: "COMPARATOR ◇", instance: "Reward prediction", formula: "{◇: DA.expected, DA.actual → RPE}", status: "blunted" },
  { gate: "LATCH ⊡", instance: "GR downregulation", formula: "{⊡: GR.downreg → cortisol_resistance.LATCHED}", status: "latched" },
  { gate: "INTEGRATOR Σ", instance: "Allostatic load", formula: "{Σ: CORT↑(repeated) → load}", status: "accumulating" },
];
const regionHeatmap = [
  { region: "PFC", DA: 38, HT: 40, NE: 55, GABA: 35, GLU: 65 },
  { region: "NAc", DA: 28, HT: 45, NE: 50, GABA: 42, GLU: 58 },
  { region: "AMY", DA: 50, HT: 32, NE: 75, GABA: 30, GLU: 78 },
  { region: "HPC", DA: 42, HT: 38, NE: 60, GABA: 35, GLU: 72 },
  { region: "VTA", DA: 35, HT: 48, NE: 45, GABA: 50, GLU: 55 },
  { region: "DRN", DA: 55, HT: 30, NE: 40, GABA: 45, GLU: 50 },
];
const doseResponseData = Array.from({ length: 20 }, (_, i) => {
  const x = i * 5;
  return {
    dose: x,
    DA_effect: Math.max(0, -(x - 50) * (x - 50) / 30 + 80),
    NE_effect: Math.max(0, -(x - 45) * (x - 45) / 25 + 85),
    CORT_effect: x < 40 ? x * 1.8 : 72 - (x - 40) * 0.5,
  };
});
const crossAnalysis = [
  { x: 82, y: 30, z: 400, label: "CORT↑↑ vs BDNF↓↓", category: "stress→plasticity" },
  { x: 82, y: 35, z: 350, label: "CORT↑↑ vs 5HT↓", category: "stress→mood" },
  { x: 71, y: 35, z: 300, label: "IL6↑ vs 5HT↓", category: "immune→mood" },
  { x: 71, y: 30, z: 280, label: "IL6↑ vs BDNF↓↓", category: "immune→plasticity" },
  { x: 42, y: 82, z: 250, label: "DA↓ vs CORT↑↑", category: "reward→stress" },
  { x: 38, y: 72, z: 220, label: "GABA↓ vs GLU↑", category: "inhibition→excitation" },
];
const failureModes = [
  { name: "HPA loop\nfailure", size: 420, severity: "critical", color: C.down },
  { name: "5HT\ndepletion", size: 350, severity: "high", color: C.orange },
  { name: "Inflammatory\nshunt", size: 300, severity: "high", color: C.orange },
  { name: "BDNF\ndepletion", size: 280, severity: "high", color: C.warn },
  { name: "eCB\ndeficit", size: 180, severity: "moderate", color: C.purple },
  { name: "GLU\nspillover", size: 150, severity: "moderate", color: C.purple },
  { name: "DA\nresistance", size: 120, severity: "low", color: C.textMuted },
];
const lifecycleComparison = [
  { stage: "Synthesis", healthy: 80, current: 40, vulnerable: true },
  { stage: "Storage", healthy: 85, current: 65, vulnerable: false },
  { stage: "Release", healthy: 75, current: 50, vulnerable: true },
  { stage: "Binding", healthy: 80, current: 60, vulnerable: false },
  { stage: "Transduction", healthy: 85, current: 55, vulnerable: true },
  { stage: "Effect", healthy: 80, current: 35, vulnerable: true },
  { stage: "Termination", healthy: 70, current: 80, vulnerable: false },
];
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
const tabs = [
  "Overview", "Signals", "Pathways", "Gates & Lifecycles", "Loops & Failures", "Trajectories", "Cross-Analysis"
];
export default function BioSpherePage() {
  const [activeTab, setActiveTab] = useState(0);
  const [selectedSignal, setSelectedSignal] = useState<string | null>(null);
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
            Person: <span style={{ color: C.text }}>Alex M.</span> · Last analysis: 2h ago · Schema v6
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <span style={{ fontSize: 10, color: C.textMuted, padding: "4px 10px", background: `${C.down}15`, border: `1px solid ${C.down}30`, borderRadius: 4 }}>
            <StatusDot status="critical" /> 2 Critical Loops
          </span>
          <span style={{ fontSize: 10, color: C.textMuted, padding: "4px 10px", background: `${C.warn}15`, border: `1px solid ${C.warn}30`, borderRadius: 4 }}>
            Trajectory: @t4 Chronic
          </span>
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
            <Card title="1 · BASELINE CHEMICAL PROFILE" subtitle="signal + profile_snapshot → radar" accent={C.accent}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ color: C.accent, fontFamily: "'IBM Plex Mono'" }}>profile_snapshot JOIN signal</code>
              </div>
              <ResponsiveContainer width="100%" height={200}>
                <RadarChart data={radarData}>
                  <PolarGrid stroke={C.border} />
                  <PolarAngleAxis dataKey="dim" tick={{ fill: C.textMuted, fontSize: 9 }} />
                  <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
                  <Radar dataKey="value" stroke={C.accent} fill={C.accent} fillOpacity={0.15} strokeWidth={2} />
                </RadarChart>
              </ResponsiveContainer>
            </Card>
            <Card title="2 · TRAJECTORY PROGRESSION" subtitle="trajectory + trajectory_phase → line chart" accent={C.warn}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ color: C.warn, fontFamily: "'IBM Plex Mono'" }}>trajectory_phase ORDER BY phase_number</code>
              </div>
              <ResponsiveContainer width="100%" height={200}>
                <LineChart data={trajectoryData}>
                  <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fill: C.textMuted, fontSize: 10 }} />
                  <YAxis domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                  <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                  <Line type="monotone" dataKey="CORT" stroke={C.down} strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="DA" stroke={C.accent} strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="HT" stroke={C.purple} strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="BDNF" stroke={C.up} strokeWidth={2} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="IL6" stroke={C.orange} strokeWidth={2} dot={{ r: 3 }} />
                </LineChart>
              </ResponsiveContainer>
              <div style={{ display: "flex", gap: 12, marginTop: 6, justifyContent: "center" }}>
                {([["CORT", C.down], ["DA", C.accent], ["5HT", C.purple], ["BDNF", C.up], ["IL6", C.orange]] as const).map(([l, c]) => (
                  <span key={l} style={{ fontSize: 9, color: c, display: "flex", alignItems: "center", gap: 3 }}>
                    <span style={{ width: 8, height: 2, background: c, display: "inline-block" }} /> {l}
                  </span>
                ))}
              </div>
            </Card>
            <Card title="3 · ACTIVE LOOPS" subtitle="active_loop → status list" accent={C.down}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ color: C.down, fontFamily: "'IBM Plex Mono'" }}>active_loop WHERE person_id = @pid</code>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {loopData.map(l => (
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
            </Card>
            <Card title="4 · SIGNAL STATES TABLE" subtitle="observation → structured columns" span={2} accent={C.cyan}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ color: C.cyan, fontFamily: "'IBM Plex Mono'" }}>observation WHERE operator, region, temporal, failure_mode filterable</code>
              </div>
              <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 11 }}>
                  <thead>
                    <tr style={{ borderBottom: `1px solid ${C.border}` }}>
                      {["Signal", "Code", "State", "Region", "Trend", "Failure"].map(h => (
                        <th key={h} style={{ padding: "6px 10px", textAlign: "left", color: C.textMuted, fontSize: 10, fontWeight: 500 }}>{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {baselineProfile.map(s => (
                      <tr key={s.signal} style={{ borderBottom: `1px solid ${C.border}`, cursor: "pointer" }}
                        onClick={() => setSelectedSignal(s.signal === selectedSignal ? null : s.signal)}>
                        <td style={{ padding: "6px 10px", fontWeight: 500 }}>{s.label}</td>
                        <td style={{ padding: "6px 10px", fontFamily: "'IBM Plex Mono'", color: C.accent, fontSize: 10 }}>
                          {s.signal === "CORT" ? "H:" : s.signal === "BDNF" || s.signal === "OXT" ? "P:" : s.signal === "IL6" ? "NI:" : s.signal === "ANA" ? "eCB:" : "NT:"}{s.signal}
                        </td>
                        <td style={{ padding: "6px 10px" }}><StateChip state={s.state} trend={s.trend} /></td>
                        <td style={{ padding: "6px 10px", fontFamily: "'IBM Plex Mono'", fontSize: 10, color: C.textMuted }}>@{s.region}</td>
                        <td style={{ padding: "6px 10px", color: s.trend === "declining" ? C.down : s.trend === "increasing" ? C.warn : C.up, fontSize: 10 }}>
                          {s.trend}
                        </td>
                        <td style={{ padding: "6px 10px", fontSize: 10, color: s.value > 70 || s.value < 35 ? C.orange : C.textDim }}>
                          {s.value > 75 ? "⚡ excess" : s.value < 35 ? "⚡ depletion" : "—"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
            <Card title="5 · FAILURE MODE SEVERITY" subtitle="observation.failure_mode + entity_tag.severity" accent={C.orange}>
              <div style={{ display: "flex", flexDirection: "column", gap: 5, marginTop: 4 }}>
                {failureModes.map(f => (
                  <div key={f.name} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{ width: 70, fontSize: 9, color: C.textMuted, textAlign: "right", whiteSpace: "pre-line", lineHeight: 1.2 }}>{f.name}</span>
                    <div style={{ flex: 1, height: 14, background: C.bg, borderRadius: 3, overflow: "hidden" }}>
                      <div style={{ width: `${f.size / 4.2}%`, height: "100%", background: f.color, borderRadius: 3, opacity: 0.7 }} />
                    </div>
                    <span style={{ fontSize: 9, color: f.color, width: 50, textAlign: "right" }}>{f.severity}</span>
                  </div>
                ))}
              </div>
            </Card>
            <Card title="6 · REGION HEATMAP" subtitle="observation JOIN brain_region → signal × region matrix" span={2} accent={C.purple}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ color: C.purple, fontFamily: "'IBM Plex Mono'" }}>observation GROUP BY region_id, signal_id → pivot</code>
              </div>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr>
                    <th style={{ padding: 6, fontSize: 10, color: C.textMuted, textAlign: "left" }}>Region</th>
                    {["DA", "5HT", "NE", "GABA", "GLU"].map(h => (
                      <th key={h} style={{ padding: 6, fontSize: 10, color: C.textMuted, textAlign: "center" }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {regionHeatmap.map(r => (
                    <tr key={r.region}>
                      <td style={{ padding: 6, fontSize: 11, fontFamily: "'IBM Plex Mono'", color: C.accent }}>{r.region}</td>
                      <HeatCell value={r.DA} />
                      <HeatCell value={r.HT} />
                      <HeatCell value={r.NE} />
                      <HeatCell value={r.GABA} />
                      <HeatCell value={r.GLU} />
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
            <Card title="7 · DOSE-RESPONSE CURVES" subtitle="dose_response → inverted-U plots" accent={C.up}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ color: C.up, fontFamily: "'IBM Plex Mono'" }}>dose_response WHERE pattern = 'INVERTED_U'</code>
              </div>
              <ResponsiveContainer width="100%" height={160}>
                <AreaChart data={doseResponseData}>
                  <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                  <XAxis dataKey="dose" tick={{ fill: C.textMuted, fontSize: 9 }} label={{ value: "Dose →", position: "insideBottom", offset: -2, fill: C.textMuted, fontSize: 9 }} />
                  <YAxis tick={{ fill: C.textMuted, fontSize: 9 }} label={{ value: "Effect", angle: -90, position: "insideLeft", fill: C.textMuted, fontSize: 9 }} />
                  <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 10 }} />
                  <Area type="monotone" dataKey="DA_effect" stroke={C.accent} fill={C.accent} fillOpacity={0.1} strokeWidth={2} />
                  <Area type="monotone" dataKey="NE_effect" stroke={C.purple} fill={C.purple} fillOpacity={0.1} strokeWidth={2} />
                </AreaChart>
              </ResponsiveContainer>
              <div style={{ display: "flex", gap: 12, justifyContent: "center", marginTop: 4 }}>
                {([["DA@PFC", C.accent], ["NE", C.purple]] as const).map(([l, c]) => (
                  <span key={l} style={{ fontSize: 9, color: c }}>● {l}</span>
                ))}
              </div>
            </Card>
          </div>
        )}
        {/* ═══ SIGNALS TAB ═══ */}
        {activeTab === 1 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="SIGNAL INTENSITY BAR CHART" subtitle="profile_snapshot.latest_intensity per signal" span={2} accent={C.accent}>
              <ResponsiveContainer width="100%" height={250}>
                <BarChart data={baselineProfile} layout="vertical">
                  <CartesianGrid stroke={C.border} strokeDasharray="3 3" horizontal={false} />
                  <XAxis type="number" domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                  <YAxis type="category" dataKey="label" width={100} tick={{ fill: C.textMuted, fontSize: 11 }} />
                  <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                  <Bar dataKey="value" radius={[0, 4, 4, 0]}>
                    {baselineProfile.map((entry) => (
                      <Cell key={entry.signal} fill={entry.value > 70 ? C.down : entry.value < 35 ? C.orange : entry.value < 45 ? C.warn : C.up} fillOpacity={0.7} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
              <div style={{ textAlign: "center", fontSize: 9, color: C.textMuted, marginTop: 4 }}>
                <span style={{ color: C.up }}>● 45-70 healthy</span>{" · "}
                <span style={{ color: C.warn }}>● 35-45 low</span>{" · "}
                <span style={{ color: C.orange }}>● &lt;35 depleted</span>{" · "}
                <span style={{ color: C.down }}>● &gt;70 excess</span>
              </div>
            </Card>
            <Card title="CASCADE IMPACT" subtitle="signal_interaction → what CORT↑↑ does downstream" accent={C.down}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                Recursive: <code style={{ fontFamily: "'IBM Plex Mono'", color: C.down }}>WITH RECURSIVE chain AS ...</code>
              </div>
              {cascadeData[0].targets.map(t => (
                <div key={t.name} style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
                  <span style={{ width: 60, fontSize: 11, fontFamily: "'IBM Plex Mono'", color: C.textMuted }}>{t.name}</span>
                  <div style={{ flex: 1, height: 10, background: C.bg, borderRadius: 3, overflow: "hidden" }}>
                    <div style={{ width: `${t.impact * 100}%`, height: "100%", background: `linear-gradient(90deg, ${C.down}, ${C.orange})`, borderRadius: 3, opacity: 0.6 }} />
                  </div>
                  <span style={{ fontSize: 10, color: C.textMuted, width: 35, textAlign: "right" }}>{(t.impact * 100).toFixed(0)}%</span>
                </div>
              ))}
              <div style={{ fontSize: 9, color: C.textMuted, marginTop: 8, fontFamily: "'IBM Plex Mono'", padding: "6px 8px", background: C.bg, borderRadius: 4 }}>
                H:CORT[↑↑] ⊣ P:BDNF @HPC (chronic) #◐<br />
                H:CORT[↑↑] ⊣ NT:5HT &lt;syn&gt; @DRN (chronic) #●<br />
                H:CORT[↑↑] → GR.resist → NI:IL6[↑] #◐
              </div>
            </Card>
            <Card title="LIFECYCLE VULNERABILITY" subtitle="lifecycle_stage healthy vs current per stage" accent={C.cyan}>
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={lifecycleComparison}>
                  <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                  <XAxis dataKey="stage" tick={{ fill: C.textMuted, fontSize: 9 }} />
                  <YAxis domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                  <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                  <Bar dataKey="healthy" fill={C.up} fillOpacity={0.3} radius={[2, 2, 0, 0]} />
                  <Bar dataKey="current" radius={[2, 2, 0, 0]}>
                    {lifecycleComparison.map((entry) => (
                      <Cell key={entry.stage} fill={entry.vulnerable ? C.orange : C.accent} fillOpacity={0.7} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
              <div style={{ textAlign: "center", fontSize: 9, color: C.textMuted }}>
                <span style={{ color: `${C.up}88` }}>█ Healthy</span>{" · "}
                <span style={{ color: C.accent }}>█ Current (ok)</span>{" · "}
                <span style={{ color: C.orange }}>█ Current (vulnerable)</span>
              </div>
            </Card>
          </div>
        )}
        {/* ═══ PATHWAYS TAB ═══ */}
        {activeTab === 2 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="PATHWAY STATUS" subtitle="pathway + pathway_step + observation → wiring status" span={2} accent={C.purple}>
              <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 11, lineHeight: 2, padding: 12, background: C.bg, borderRadius: 6 }}>
                <div style={{ color: C.textMuted, fontSize: 10, marginBottom: 8 }}>SQL: pathway JOIN pathway_step JOIN observation → per-step status</div>
                <div>
                  <span style={{ color: C.warn }}>PATHWAY</span> <span style={{ color: C.accent }}>HPA_axis</span> (AMY → PVN → PIT → ADR)
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.up }}>●</span> AMY.threat_detect <span style={{ color: C.textMuted }}>═══→</span> <span style={{ color: C.up }}>●</span> CRH↑(PVN) <span style={{ color: C.textMuted }}>── normal</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.up }}>●</span> CRH + AVP <span style={{ color: C.textMuted }}>══{"{"}&sup2;{"}"} ══→</span> <span style={{ color: C.warn }}>●</span> ACTH↑↑(PIT) <span style={{ color: C.warn }}>── elevated</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.warn }}>●</span> ACTH <span style={{ color: C.textMuted }}>═══→</span> <span style={{ color: C.down }}>●</span> CORT↑↑(ADR) <span style={{ color: C.down }}>── excess</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.down }}>✗</span> CORT <span style={{ color: C.down }}>──⟳⁻──→</span> <span style={{ color: C.down }}>✗</span> GR(PVN) <span style={{ color: C.down }}>── ⚡ BROKEN</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.down }}>✗</span> CORT <span style={{ color: C.down }}>──⟳⁻──→</span> <span style={{ color: C.orange }}>◐</span> GR(HPC) <span style={{ color: C.orange }}>── degraded</span>
                </div>
                <div style={{ marginTop: 12 }}>
                  <span style={{ color: C.warn }}>PATHWAY</span> <span style={{ color: C.accent }}>mesolimbic_DA</span> (VTA → NAc)
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.orange }}>●</span> TYR <span style={{ color: C.textMuted }}>──{"{"}&diams;TH{"}"} ──→</span> <span style={{ color: C.orange }}>●</span> DA(VTA) <span style={{ color: C.orange }}>── synthesis↓</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.orange }}>●</span> DA.release <span style={{ color: C.textMuted }}>═══→</span> <span style={{ color: C.down }}>●</span> D1/D2(NAc) <span style={{ color: C.down }}>── DA↓@NAc</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.up }}>●</span> D2.auto <span style={{ color: C.textMuted }}>──⟳⁻──→</span> <span style={{ color: C.up }}>●</span> release_gate <span style={{ color: C.up }}>── intact</span>
                </div>
                <div style={{ marginTop: 12 }}>
                  <span style={{ color: C.warn }}>PATHWAY</span> <span style={{ color: C.accent }}>inflammatory_shunt</span> (immune → DRN)
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.down }}>●</span> IL6↑ <span style={{ color: C.textMuted }}>══{"{"}&sup3;{"}"} ══→</span> <span style={{ color: C.down }}>●</span> IDO↑ <span style={{ color: C.down }}>── ⚡ shunt active</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.down }}>●</span> TRP <span style={{ color: C.down }}>──→KYN──→</span> <span style={{ color: C.down }}>●</span> QUIN↑ <span style={{ color: C.down }}>── neurotoxic</span>
                </div>
                <div style={{ marginLeft: 16 }}>
                  <span style={{ color: C.down }}>●</span> TRP diverted <span style={{ color: C.textMuted }}>══→</span> <span style={{ color: C.down }}>●</span> 5HT↓↓(DRN) <span style={{ color: C.down }}>── depleted</span>
                </div>
              </div>
            </Card>
          </div>
        )}
        {/* ═══ GATES & LIFECYCLES TAB ═══ */}
        {activeTab === 3 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="GATE STATUS" subtitle="gate + gate_instance + observation → gate health" span={2} accent={C.cyan}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8 }}>
                {gateData.map(g => (
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
            </Card>
            <Card title="5HT LIFECYCLE STAGES" subtitle="lifecycle_stage WHERE signal = 5HT" accent={C.purple}>
              <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 10, lineHeight: 2, padding: 8, background: C.bg, borderRadius: 4 }}>
                <div><span style={{ color: C.orange }}>⟨syn⟩</span> TRP →<span style={{ color: C.down }}>⧫TPH2</span>→ 5HTP → 5HT <span style={{ color: C.down }}>⚡ shunted</span></div>
                <div><span style={{ color: C.up }}>⟨pkg⟩</span> 5HT →VMAT2→ vesicle <span style={{ color: C.up }}>ok</span></div>
                <div><span style={{ color: C.warn }}>⟨trg⟩</span> {"{"}&sup2;:AP,Ca²⁺ | ⊞:1A.auto{"}"} <span style={{ color: C.warn }}>gated</span></div>
                <div><span style={{ color: C.warn }}>⟨rel⟩</span> 5HT → cleft <span style={{ color: C.warn }}>reduced</span></div>
                <div><span style={{ color: C.up }}>⟨bnd⟩</span> 5HT → 1A,2A,2C,3,4,7 <span style={{ color: C.up }}>ok</span></div>
                <div><span style={{ color: C.warn }}>⟨txd⟩</span> Gi/Gq cascades <span style={{ color: C.warn }}>low input</span></div>
                <div><span style={{ color: C.down }}>⟨eff⟩</span> mood,impulse,sleep <span style={{ color: C.down }}>impaired</span></div>
                <div><span style={{ color: C.up }}>⟨trm⟩</span> SERT reuptake <span style={{ color: C.up }}>normal</span></div>
                <div><span style={{ color: C.warn }}>⟨fbk⟩</span> 1A.auto <span style={{ color: C.warn }}>functional</span></div>
              </div>
            </Card>
            <Card title="DA LIFECYCLE STAGES" subtitle="lifecycle_stage WHERE signal = DA" accent={C.accent}>
              <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 10, lineHeight: 2, padding: 8, background: C.bg, borderRadius: 4 }}>
                <div><span style={{ color: C.warn }}>⟨syn⟩</span> TYR →<span style={{ color: C.warn }}>⧫TH</span>→ L-DOPA → DA <span style={{ color: C.warn }}>stressed</span></div>
                <div><span style={{ color: C.up }}>⟨pkg⟩</span> DA →VMAT2→ vesicle <span style={{ color: C.up }}>ok</span></div>
                <div><span style={{ color: C.up }}>⟨trg⟩</span> {"{"}&sup2;:AP,Ca²⁺{"}"} <span style={{ color: C.up }}>ok</span></div>
                <div><span style={{ color: C.orange }}>⟨rel⟩</span> DA → cleft <span style={{ color: C.orange }}>reduced</span></div>
                <div><span style={{ color: C.up }}>⟨bnd⟩</span> DA → D1-D5 <span style={{ color: C.up }}>ok</span></div>
                <div><span style={{ color: C.warn }}>⟨txd⟩</span> cAMP/PKA cascade <span style={{ color: C.warn }}>dampened</span></div>
                <div><span style={{ color: C.orange }}>⟨eff⟩</span> motivation,reward <span style={{ color: C.orange }}>low</span></div>
                <div><span style={{ color: C.up }}>⟨trm⟩</span> DAT/COMT <span style={{ color: C.up }}>normal</span></div>
                <div><span style={{ color: C.up }}>⟨fbk⟩</span> D2.auto <span style={{ color: C.up }}>intact</span></div>
              </div>
            </Card>
          </div>
        )}
        {/* ═══ LOOPS & FAILURES TAB ═══ */}
        {activeTab === 4 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="FEEDBACK LOOP MAP" subtitle="active_loop → visual loop diagram" span={2} accent={C.down}>
              <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 11, lineHeight: 1.8, padding: 16, background: C.bg, borderRadius: 6 }}>
                <div style={{ marginBottom: 12 }}>
                  <span style={{ color: C.down }}>⟳⁺ VICIOUS</span> <span style={{ color: C.textMuted }}>— HPA Loop Failure (LATCHED ⊡)</span>
                </div>
                <div style={{ marginLeft: 8, borderLeft: `2px solid ${C.down}40`, paddingLeft: 12 }}>
                  CORT[↑↑] → GR.downreg@HPC → ⟳⁻ <span style={{ color: C.down, textDecoration: "line-through" }}>BROKEN</span><br />
                  → CRH[↑↑]@PVN → ACTH↑ → CORT↑↑ <span style={{ color: C.down }}>← feeds back</span><br />
                  → <span style={{ color: C.down }}>⊡ LATCHED — self-maintaining without trigger</span>
                </div>
                <div style={{ marginBottom: 12, marginTop: 16 }}>
                  <span style={{ color: C.orange }}>⟳⁺ VICIOUS</span> <span style={{ color: C.textMuted }}>— Inflammatory-Serotonin Shunt</span>
                </div>
                <div style={{ marginLeft: 8, borderLeft: `2px solid ${C.orange}40`, paddingLeft: 12 }}>
                  IL6[↑] → ⊃IDO → TRP→KYN(shunt) → 5HT[↓↓]<br />
                  → BDNF[↓] → neuroplasticity↓ → vulnerability↑<br />
                  → CORT↑ (stress) → GR.resist → anti-inflammatory↓ → IL6↑ <span style={{ color: C.orange }}>← feeds back</span>
                </div>
                <div style={{ marginBottom: 12, marginTop: 16 }}>
                  <span style={{ color: C.up }}>⟳⁺ VIRTUOUS</span> <span style={{ color: C.textMuted }}>— OXT-DA Social Reward (intact but underused)</span>
                </div>
                <div style={{ marginLeft: 8, borderLeft: `2px solid ${C.up}40`, paddingLeft: 12 }}>
                  OXT → DA↑@VTA → social_reward → approach → OXT↑<br />
                  <span style={{ color: C.up }}>Status: intact — potential intervention target</span>
                </div>
                <div style={{ marginBottom: 12, marginTop: 16 }}>
                  <span style={{ color: C.textDim }}>⟳⁺ DORMANT</span> <span style={{ color: C.textMuted }}>— Exercise-BDNF (inactive)</span>
                </div>
                <div style={{ marginLeft: 8, borderLeft: `2px solid ${C.textDim}40`, paddingLeft: 12, color: C.textDim }}>
                  exercise → βEND↑ → BDNF↑ → DA↑ → motivation → exercise<br />
                  Status: dormant — activation would break multiple vicious loops
                </div>
              </div>
            </Card>
          </div>
        )}
        {/* ═══ TRAJECTORIES TAB ═══ */}
        {activeTab === 5 && (
          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14 }}>
            <Card title="STRESS TRAJECTORY" subtitle="trajectory + trajectory_phase → temporal progression" accent={C.warn}>
              <ResponsiveContainer width="100%" height={260}>
                <AreaChart data={trajectoryData}>
                  <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fill: C.textMuted, fontSize: 11 }} />
                  <YAxis domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }} />
                  <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }} />
                  <Area type="monotone" dataKey="CORT" stroke={C.down} fill={C.down} fillOpacity={0.1} strokeWidth={2} />
                  <Area type="monotone" dataKey="IL6" stroke={C.orange} fill={C.orange} fillOpacity={0.08} strokeWidth={2} />
                  <Area type="monotone" dataKey="DA" stroke={C.accent} fill={C.accent} fillOpacity={0.08} strokeWidth={2} />
                  <Area type="monotone" dataKey="HT" stroke={C.purple} fill={C.purple} fillOpacity={0.08} strokeWidth={2} />
                  <Area type="monotone" dataKey="BDNF" stroke={C.up} fill={C.up} fillOpacity={0.08} strokeWidth={2} />
                </AreaChart>
              </ResponsiveContainer>
              <div style={{ display: "flex", gap: 12, justifyContent: "center", marginTop: 6 }}>
                {([["CORT", C.down], ["IL6", C.orange], ["DA", C.accent], ["5HT", C.purple], ["BDNF", C.up]] as const).map(([l, c]) => (
                  <span key={l} style={{ fontSize: 9, color: c }}>● {l}</span>
                ))}
              </div>
            </Card>
            <Card title="PHASE DETAIL" subtitle="trajectory_phase.state_snapshot" accent={C.warn}>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {[
                  { label: "@t0 Baseline", color: C.up, text: "DA≈ 5HT≈ NE≈ CORT≈ BDNF≈", status: "✓" },
                  { label: "@t1 Acute", color: C.warn, text: "DA↑ NE↑↑ CORT↑ 5HT≈", status: "✓" },
                  { label: "@t2 Repeated", color: C.orange, text: "DA↓ 5HT↓ CORT↑ BDNF↓ IL6◊", status: "✓" },
                  { label: "@t3 Chronic", color: C.down, text: "DA↓↓ 5HT↓↓ CORT↑↑ BDNF↓↓ IL6↑", status: "✓" },
                  { label: "@t4 Current", color: C.down, text: "DA↓↓ 5HT↓↓ CORT↑↑ BDNF↓↓ IL6↑↑", status: "◄" },
                  { label: "@t5 Predicted", color: C.textDim, text: "If unchecked: ⊡ latched states", status: "?" },
                ].map(p => (
                  <div key={p.label} style={{
                    padding: "8px 10px", borderRadius: 4, background: `${p.color}08`,
                    border: `1px solid ${p.color}20`, fontSize: 10,
                  }}>
                    <div style={{ fontWeight: 600, color: p.color, marginBottom: 2 }}>
                      {p.status} {p.label}
                    </div>
                    <div style={{ fontFamily: "'IBM Plex Mono'", color: C.textMuted, fontSize: 9 }}>
                      {p.text}
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          </div>
        )}
        {/* ═══ CROSS-ANALYSIS TAB ═══ */}
        {activeTab === 6 && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card title="CROSS-SIGNAL CORRELATION" subtitle="observation × observation → scatter plot of signal pairs" span={2} accent={C.pink}>
              <div style={{ fontSize: 10, color: C.textMuted, marginBottom: 8 }}>
                SQL: <code style={{ fontFamily: "'IBM Plex Mono'", color: C.pink }}>
                  observation a JOIN observation b ON a.personality_id = b.personality_id → signal correlations over time
                </code>
              </div>
              <ResponsiveContainer width="100%" height={280}>
                <ScatterChart>
                  <CartesianGrid stroke={C.border} strokeDasharray="3 3" />
                  <XAxis type="number" dataKey="x" name="Source Signal" domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }}
                    label={{ value: "Source signal level →", position: "insideBottom", offset: -5, fill: C.textMuted, fontSize: 10 }} />
                  <YAxis type="number" dataKey="y" name="Target Signal" domain={[0, 100]} tick={{ fill: C.textMuted, fontSize: 10 }}
                    label={{ value: "Target signal level →", angle: -90, position: "insideLeft", fill: C.textMuted, fontSize: 10 }} />
                  <ZAxis type="number" dataKey="z" range={[60, 300]} />
                  <Tooltip contentStyle={{ background: C.surface, border: `1px solid ${C.border}`, borderRadius: 6, fontSize: 11 }}
                    formatter={(v: number, name: string) => [v, name]}
                    labelFormatter={() => ""} />
                  <Scatter data={crossAnalysis} fill={C.pink} fillOpacity={0.6}>
                    {crossAnalysis.map((e, i) => (
                      <Cell key={i} fill={e.category.includes("stress") ? C.down : e.category.includes("immune") ? C.orange : e.category.includes("reward") ? C.accent : C.purple} />
                    ))}
                  </Scatter>
                </ScatterChart>
              </ResponsiveContainer>
              <div style={{ display: "flex", gap: 16, justifyContent: "center", marginTop: 6 }}>
                {([["stress→", C.down], ["immune→", C.orange], ["reward→", C.accent], ["inhibition→", C.purple]] as const).map(([l, c]) => (
                  <span key={l} style={{ fontSize: 9, color: c }}>● {l}</span>
                ))}
              </div>
            </Card>
            <Card title="CROSS-ANALYSIS QUERIES THIS ENABLES" subtitle="Combining tables across analyses" span={2} accent={C.accent}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, fontSize: 11 }}>
                {[
                  { q: "Which loops are driving the trajectory?", sql: "active_loop JOIN trajectory ON involved_signals ∩ observation_ids", icon: "🔗" },
                  { q: "What gate states changed between phases?", sql: "gate_instance JOIN observation WHERE analysis_run_id IN (trajectory phases)", icon: "⊼" },
                  { q: "Which pathways are most degraded?", sql: "pathway_step JOIN observation → aggregate failure_mode per pathway", icon: "⚡" },
                  { q: "What's the cascade if we fix CORT?", sql: "WITH RECURSIVE signal_interaction WHERE source = CORT → walk graph", icon: "🌊" },
                  { q: "Find people with similar loop patterns", sql: "active_loop embedding <=> query_embedding → cosine similarity", icon: "👥" },
                  { q: "Which lifecycle stage is most vulnerable?", sql: "lifecycle_stage JOIN observation → min(intensity) per stage", icon: "🔬" },
                  { q: "Compare two people's region heatmaps", sql: "observation GROUP BY person_id, region_id, signal_id → pivot both", icon: "🗺" },
                  { q: "What phenotype tags cluster together?", sql: "entity_tag JOIN entity_tag ON entity_id → co-occurrence matrix", icon: "🏷" },
                  { q: "Predict next trajectory phase", sql: "trajectory_phase.state_embedding <=> embedding_cache(known_phases)", icon: "🔮" },
                  { q: "Which dose-response curve are they on?", sql: "dose_response JOIN profile_snapshot → current position on curve", icon: "📈" },
                ].map(item => (
                  <div key={item.q} style={{ padding: "10px 12px", background: C.bg, borderRadius: 6, border: `1px solid ${C.border}` }}>
                    <div style={{ fontSize: 14, marginBottom: 4 }}>{item.icon}</div>
                    <div style={{ fontWeight: 500, marginBottom: 4 }}>{item.q}</div>
                    <div style={{ fontFamily: "'IBM Plex Mono'", fontSize: 9, color: C.textMuted, wordBreak: "break-all" }}>{item.sql}</div>
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
