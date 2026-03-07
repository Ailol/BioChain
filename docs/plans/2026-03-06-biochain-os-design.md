# BioChain OS — Multi-Canvas Dashboard Design

## Vision

Transform the single force-directed graph into a **multi-projection neuro-symbolic operating surface** with 6 synchronized views, each using its optimal rendering engine. Every entity in the system is reachable from multiple angles — the same DA signal appears as a node in the circuit map, a row in the gate board, a phase-lane in the corridor, and a contributor in the bind dashboard.

## Data Architecture — 5 Layers

| Layer | Content | Source | Mutability |
|-------|---------|--------|------------|
| **Atlas** | Stable biology — signals, receptors, transporters, limiters, regions, edges | DB graph views (`v_system`, `v_graph`) | Slow (new analyses) |
| **Protocol** | BNF grammar trace — gates, formulas, feedback loops, cascades | DB (`evaluate_gate()`, `find_feedback_loops()`, `find_dysreg_cascades()`) | Computed on read |
| **Execution** | State machine — gate armed/active/latched/decaying, signal states, cascade propagation | DB states + real-time BFS | Session-mutable |
| **Phenotype** | Computed behavioral lens — binds, motif scores, stability indices, fragility halos | LLM analysis + graph metrics | Computed per-analysis |
| **Signature** | Person-specific over time — trajectories, baselines, phase history | Future: temporal DB tables | Append-only |

## Three-Tiered Node Architecture

| Tier | Examples | Visual Treatment | Interaction |
|------|----------|-----------------|-------------|
| **Hard nodes** | Signals (DA, 5HT), Receptors, Transporters, Limiters, Regions | Solid fill, consistent size | Click → structured drill |
| **Logic objects** | Gates, Feedback loops, Dysreg cascades | Distinct shape (diamond, ring), border emphasis | Click → condition panel |
| **Computed projections** | Binds, Motif scores, Stability indices | Translucent, halo/glow | Click → contribution breakdown |

## Layout — 2x3 Grid with Shared Selection

```
┌─────────────────────────────────────────────────────────────┐
│ [BioChain OS]  Subject: {name}   ◉ Focused: DA_VTA          │
│ ┌──────────┬──────────┬──────────┐                          │
│ │  Motif   │ Regional │   Gate   │  ← Row 1: Structure      │
│ │  Galaxy  │ Circuit  │  Board   │                          │
│ │          │   Map    │          │                          │
│ ├──────────┼──────────┼──────────┤                          │
│ │  Phase   │  Bind    │ Explain  │  ← Row 2: Dynamics       │
│ │ Corridor │Dashboard │  Core    │                          │
│ │          │          │          │                          │
│ └──────────┴──────────┴──────────┘                          │
│ [Status bar: geometry metrics | feedback loops | cascades]   │
└─────────────────────────────────────────────────────────────┘
```

Each cell is a `<ViewPanel>` component with shared props:
- `graph: ConstellationGraphResponse`
- `analysis: ConstellationAnalysisResponse`
- `focus: FocusState` (shared selection context)
- `onFocus: (entity: FocusTarget) => void`

### Shared Selection State (Zustand)

```typescript
interface FocusState {
  entity: string | null;          // node ID: "signal:42"
  phase: string | null;           // "acute" | "sustained" | "compensated" | ...
  motif: string | null;           // motif ID
  bind: string | null;            // bind ID
  gate: string | null;            // gate ID
  community: number | null;       // community ID
  cascadeNodes: Set<string>;      // BFS cascade result
}

type FocusTarget =
  | { type: 'entity'; id: string }
  | { type: 'phase'; phase: string }
  | { type: 'motif'; id: string }
  | { type: 'bind'; id: string }
  | { type: 'gate'; id: string }
  | { type: 'community'; id: number }
  | { type: 'clear' };
```

Clicking anything in any view dispatches `onFocus(target)` → all 6 views highlight the relevant entity/group.

---

## View 1: Motif Galaxy (Top-Left)

**Renderer:** Sigma.js + Graphology (reuse existing graph engine)
**Layout:** ForceAtlas2 with community-aware clustering

This is the evolution of the current constellation graph. Key changes:

### Nodes
- **Bigger base sizes** (already implemented: signal=12, bind=14, receptor=8, gate=8)
- **State labels visible** on all nodes (already implemented)
- **Three-tier visual treatment**: hard nodes solid, gates diamond-shaped, binds translucent with glow
- **Fragility halos**: ring around binds/motifs colored by stability (green=stable, yellow=costly, orange=brittle, red=unstable)

### Edges
- Straight → Causal (already implemented)
- Curved → Feedback (already implemented via `@sigma/edge-curve`)
- Dashed → Dysreg (already implemented via canvas overlay)
- Dotted → Bind (already implemented via canvas overlay)
- **NEW: Compensation bridges** — double-line overlay connecting damaged primary path + alternate route, drawn in the edge canvas layer

### Motif Capsules
Repeated patterns (HPA latch, DA depletion loop, serotonergic compensation) rendered as **expandable grouped nodes**:
- **Collapsed**: Single capsule node with motif icon + name, size proportional to instance count
- **Expanded**: Click to expand and show constituent nodes with internal edges
- Implementation: graphology subgraph grouping → sigma `nodeReducer` swaps between capsule and expanded views

### Community Nebulae
Keep existing radial gradient nebulae (already implemented in `drawNebulae()`). Add:
- **Status indicator**: border color matches community status (green/yellow/orange/red)
- **Fragmentation metric**: dashed border when `geometry.fragmentation > 0.6`

### Interactions
- **Click node** → `onFocus({ type: 'entity', id })` → highlights in all views
- **Double-click node** → cascade BFS (existing) + highlight cascade path in Phase Corridor
- **Click motif capsule** → `onFocus({ type: 'motif', id })` → expand capsule + highlight in other views
- **Right-click** → context menu: "Show in Gate Board", "Show in Phase Corridor", "Show Upstream/Downstream"

---

## View 2: Regional Circuit Map (Top-Center)

**Renderer:** SVG (React components, no external lib)
**Layout:** Fixed anatomical arrangement

Brain regions as rectangular **frames** arranged in anatomical approximation:

```
┌─────────────────────────────────┐
│              PFC                │
│  ┌─────┐ ┌─────┐ ┌─────┐      │
│  │ DA  │ │ 5HT │ │ NE  │      │
│  └─────┘ └─────┘ └─────┘      │
├─────────────┬───────────────────┤
│    AMY      │      HPC         │
│  ┌─────┐    │   ┌─────┐       │
│  │GABA │    │   │ BDNF│       │
│  │ CRH │    │   │ GLU │       │
│  └─────┘    │   └─────┘       │
├─────────────┼───────────────────┤
│    VTA      │      DRN         │
│  ┌────┐     │   ┌────┐        │
│  │ DA │     │   │5HT │        │
│  └────┘     │   └────┘        │
├─────────────┴───────────────────┤
│  PVN    │    ADR    │   SCN    │
│  OXT    │   CORT    │  melat   │
│  CRH    │   NE      │         │
└─────────┴───────────┴──────────┘
```

### Signal Pills Inside Frames
Each signal rendered as a small pill inside its region frame:
- Color: `TYPE_COLORS[type]` (same palette as graph)
- Text: `code` + `stateIcon(state)`
- Size: proportional to weight
- Click → `onFocus({ type: 'entity', id })`

### Inter-Frame Flows (Interfaces)
Interface nodes become **animated SVG arrows** between region frames:
- Arrow from source region frame to target region frame
- Label: interface code
- Color: based on operator class
- Animated dash-offset for active flows

### Region Frame Styling
- Background: subtle gradient based on overall region health (computed from signal states within)
- Border: colored by community membership of majority signals
- Header: region code + signal count

### Focus Highlight
When `focus.entity` is set and the node is in this view:
- Pulse animation on the signal pill
- Highlight all edges connected to it
- Dim other region frames (opacity 0.3)

---

## View 3: Gate Board (Top-Right)

**Renderer:** React components (table/card layout)
**Layout:** Sorted list/grid of gates

Gates as first-class objects in a **switchboard** UI:

```
┌──────────────────────────────────────────┐
│ GATE BOARD                    [filter▾]  │
├──────────────────────────────────────────┤
│ ┌────────────────────────────────────┐   │
│ │ 🔴 DA_VTA_gate          LATCHED   │   │
│ │ threshold: 0.7  current: 0.85     │   │
│ │ ████████████████████░░░░ 85%      │   │
│ │ condition: DA_VTA > 0.7           │   │
│ │ inputs: [DA_VTA ↑↑] [NE_LC ↑]    │   │
│ │ output: → AMY_GABA inhibition     │   │
│ │ latch: ON (since acute phase)     │   │
│ └────────────────────────────────────┘   │
│ ┌────────────────────────────────────┐   │
│ │ 🟡 5HT_DRN_gate         ARMED    │   │
│ │ threshold: 0.5  current: 0.48     │   │
│ │ ████████████████░░░░░░░░ 48%      │   │
│ │ condition: 5HT_DRN > 0.5         │   │
│ │ inputs: [5HT_DRN ≈] [CORT ↑]    │   │
│ │ output: → PFC mood regulation     │   │
│ │ latch: OFF                        │   │
│ └────────────────────────────────────┘   │
└──────────────────────────────────────────┘
```

### Gate States
- **Armed** (yellow): approaching threshold
- **Active** (green): threshold exceeded, firing
- **Latched** (red): locked in active state
- **Decaying** (gray): post-firing cooldown
- **Composite** (blue): multiple conditions combined

### Data Source
`evaluate_gate()` SQL function already computes gate state. Currently returned in `v_graph` edges but not surfaced for individual gates. Need new query or extend `export_graph_json()` to include gate evaluation details.

### Interaction
- Click gate → `onFocus({ type: 'gate', id })` → highlight input/output nodes in all views
- Toggle gate state (what-if mode) → re-run cascade simulation

---

## View 4: Phase Corridor (Bottom-Left)

**Renderer:** CSS Grid or SVG
**Layout:** Horizontal timeline with vertical signal lanes

```
         baseline │ acute │ sustained │ compensated │ chronic │ recovery
    ─────────────┼───────┼───────────┼─────────────┼─────────┼──────────
    DA_VTA    ≈  │  ↑↑   │    ↑      │     ≈       │   ↓     │    ≈
    ─────────────┼───────┼───────────┼─────────────┼─────────┼──────────
    5HT_DRN   ≈  │  ↓    │    ↓↓     │     ↓       │   ↓↓    │    ↓
    ─────────────┼───────┼───────────┼─────────────┼─────────┼──────────
    CORT_ADR  ≈  │  ↑↑↑  │    ↑↑     │     ↑       │   ↑↑    │    ↑
    ─────────────┼───────┼───────────┼─────────────┼─────────┼──────────
    GABA_AMY  ≈  │  ↓    │    ↓      │     ↑       │   ≈     │    ≈
```

### Phase Definitions
Columns represent temporal phases. Currently the backend doesn't produce phase data — this requires either:
1. **LLM inference**: Add phase assignment to the constellation analysis prompt
2. **Heuristic**: Derive phases from gate states + cascade depth + tau values

### Signal Lanes
- Each row = one signal (filtered by `visibleKinds`)
- Cell color = state severity (gradient from green ≈ to red ↑↑/↓↓)
- Cell content = state icon + optional delta text

### Gate Events
Vertical markers at phase transitions showing which gates fired:
- Diamond icon at the column boundary
- Tooltip: gate name + condition + triggered signal changes

### Focus Highlight
- `focus.entity` → highlight that signal's lane
- `focus.phase` → highlight that column
- `focus.motif` → highlight all signals in motif across phases

### Data Requirements (NEW)
Need to extend `ConstellationAnalysisResponse` with:
```typescript
interface PhaseData {
  phases: string[];                              // ordered phase names
  signalPhases: Record<string, Record<string, string>>;  // signalCode → phase → state
  gateEvents: { phase: string; gate: string; trigger: string }[];
}
```

---

## View 5: Bind Dashboard (Bottom-Center)

**Renderer:** React + SVG bars (or Recharts)
**Layout:** Card grid with contribution breakdowns

Binds as **computed dashboards** with live readouts:

```
┌──────────────────────────────────────────┐
│ BIND DASHBOARD                           │
├──────────────────────────────────────────┤
│ ┌────────────────────────────────────┐   │
│ │ Mood Regulation         score: 72  │   │
│ │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░ 72%       │   │
│ │                                    │   │
│ │ Contributors:                      │   │
│ │ DA_VTA   ████████████  +45%  ↑↑   │   │
│ │ 5HT_DRN  ████████     -25%  ↓    │   │
│ │ NE_LC    ██████        +18%  ↑    │   │
│ │ GABA_AMY  ███          +12%  ≈    │   │
│ │                                    │   │
│ │ Fragility: 🟡 Costly              │   │
│ │ Stability: compensation active     │   │
│ │ Compensator: NE_LC masking 5HT    │   │
│ └────────────────────────────────────┘   │
│ ┌────────────────────────────────────┐   │
│ │ Stress Response         score: 84  │   │
│ │ ...                                │   │
│ └────────────────────────────────────┘   │
└──────────────────────────────────────────┘
```

### Contribution Bars
Each bind shows horizontal bars for contributing signals:
- Bar length: proportional to contribution weight
- Bar color: `TYPE_COLORS[type]`
- Label: signal code + delta percentage + state icon
- Data source: bind edges in graph (`operatorClass === 'bind'`) + edge weights

### Fragility Halo
Derived from:
- Number of compensators masking signals in this bind
- Dysreg cascades touching contributing signals
- Gate latch states affecting inputs

Color scale: green (stable) → yellow (costly) → orange (brittle) → red (unstable)

### Interactions
- Click bind → `onFocus({ type: 'bind', id })` → highlight all contributing signals in all views
- Click contributor signal → `onFocus({ type: 'entity', id })`
- Hover bar → tooltip with full signal details

---

## View 6: Explanation Core (Bottom-Right)

**Renderer:** React components (rich text + node chips)
**Layout:** Scrollable narrative panel

This consolidates the current sidebar panels (Systems, Person, Architecture) into a unified explanation view that responds to focus:

### Adaptive Content
The panel content changes based on `focus`:

| Focus | Content |
|-------|---------|
| No focus | Executive summary: top 3 narratives, geometry metrics, overall health |
| Entity focused | **Structured drill**: upstream causes, downstream consequences, gating conditions, feedback loops, compensators, phase behavior |
| Community focused | Community card: status, whenWorking/whenBroken, signal chips, fix protocol |
| Motif focused | Motif detail: pattern, instances, meaning, expansion |
| Bind focused | Bind detail: contribution breakdown, fragility analysis, compensator info |
| Gate focused | Gate detail: condition, inputs, outputs, latch state, history |

### Structured Drill (entity focus)
When a node is focused, show a structured traversal instead of just "expanded neighborhood":

```
┌──────────────────────────────────────┐
│ DA_VTA — Dopamine (VTA)        ↑↑   │
│ weight: 11  centrality: 0.34        │
│                                      │
│ ▸ UPSTREAM (3 causes)               │
│   • NE_LC → causal+ → DA_VTA       │
│   • GLU_PFC → causal+ → DA_VTA     │
│   • CORT_ADR → feedback- → DA_VTA  │
│                                      │
│ ▸ DOWNSTREAM (5 effects)            │
│   • DA_VTA → causal+ → GABA_NAc    │
│   • DA_VTA → causal+ → mood_reg    │
│   ...                                │
│                                      │
│ ▸ GATING CONDITIONS (2 gates)       │
│   • DA_VTA_gate: LATCHED (0.85)     │
│   • AMY_inhibition: ARMED (0.48)    │
│                                      │
│ ▸ FEEDBACK LOOPS (1 loop)           │
│   DA_VTA → NAc → VTA → DA_VTA      │
│   Type: positive, Status: active     │
│                                      │
│ ▸ COMPENSATORS                       │
│   NE_LC compensating for 5HT_DRN    │
│   Cost: elevated norepinephrine     │
│                                      │
│ ▸ PHASE BEHAVIOR                    │
│   acute: ↑↑  sustained: ↑  comp: ≈ │
└──────────────────────────────────────┘
```

All data comes from existing graph/analysis response — no new backend calls needed. The drill is computed client-side by traversing the graphology graph + matching against analysis data.

---

## New Conceptual Objects

### Compensation Bridges
**Data source:** `analysis.compensators`
**Visual:** Double-line SVG path in Motif Galaxy view connecting:
1. Damaged primary path (dashed, red)
2. Alternate compensating route (solid, yellow)
3. Hidden cost annotation (text label)

### Motif Capsules
**Data source:** `analysis.motifs`
**Visual:** Grouped node in Motif Galaxy that can expand/collapse
**State:** `expandedMotifs: Set<string>` in store

### Fragility Halos
**Data source:** Computed from compensator count + dysreg cascades + gate states per bind/motif
**Visual:** Colored ring in Motif Galaxy + colored badge in Bind Dashboard

### Phase Splits
**Data source:** NEW — needs LLM analysis extension
**Visual:** Phase Corridor column transitions

### Masked Dysreg
**Data source:** `analysis.compensators[].masking`
**Visual:** Dashed overlay in Motif Galaxy + warning icon in Bind Dashboard

---

## Backend Changes Needed

### 1. Extend Graph Response (minor)
Add gate evaluation details to `export_graph_json()`:
```sql
-- Add gate_evaluations array to JSON
SELECT json_agg(json_build_object(
  'gateId', g.entity_id,
  'code', g.code,
  'gateType', g.gate_type,
  'state', evaluate_gate(g.entity_id, p_subject_id),
  'threshold', g.threshold,
  'inputs', (SELECT json_agg(...) FROM edge WHERE target_id = g.entity_id),
  'outputs', (SELECT json_agg(...) FROM edge WHERE source_id = g.entity_id)
)) FROM gate g WHERE g.entity_id IN (subject's gates)
```

### 2. Extend Analysis Prompt (medium)
Add to `ConstellationAnalysisPrompt`:
- Phase assignment per signal
- Gate event timeline
- Fragility scores per bind

### 3. New TypeScript Types
```typescript
interface GateEvaluation {
  gateId: string;
  code: string;
  gateType: string;
  state: 'armed' | 'active' | 'latched' | 'decaying';
  threshold: number;
  currentValue: number;
  inputs: { node: string; operator: string }[];
  outputs: { node: string; operator: string }[];
}

interface PhaseAssignment {
  phases: string[];
  signalPhases: Record<string, Record<string, string>>;
  gateEvents: { phase: string; gate: string; trigger: string }[];
}
```

---

## Implementation Phases

### Phase 1: Foundation (This PR)
**Goal:** Layout shell + shared selection state + enhanced Motif Galaxy

1. Refactor `constellationStore.ts` — add `FocusState`, replace single-entity selection
2. Create `<ViewPanel>` wrapper component with title bar + resize handle
3. Create 2x3 grid layout in `ConstellationPage.tsx`
4. Move existing Sigma.js graph into `<MotifGalaxy>` component (View 1)
5. Add compensation bridge overlay to edge canvas
6. Implement motif capsule expand/collapse

### Phase 2: Static Views
**Goal:** Gate Board + Bind Dashboard + Regional Circuit Map

7. Build `<GateBoard>` (View 3) — card layout from existing gate data
8. Build `<BindDashboard>` (View 5) — contribution bars from bind edges
9. Build `<RegionalCircuitMap>` (View 2) — SVG region frames + signal pills
10. Wire cross-view focus highlighting

### Phase 3: Dynamic Views
**Goal:** Phase Corridor + Explanation Core

11. Extend backend analysis prompt with phase assignment
12. Build `<PhaseCorridor>` (View 4) — CSS Grid timeline
13. Build `<ExplanationCore>` (View 6) — adaptive narrative panel with structured drill
14. Wire all 6 views to shared `FocusState`

### Phase 4: Polish
**Goal:** Fragility halos, animations, responsive layout

15. Compute fragility scores client-side
16. Add fragility halo rendering to Motif Galaxy + Bind Dashboard
17. Animate inter-view transitions (focus changes)
18. Responsive: stack views vertically on narrow screens
19. Keyboard navigation between views

---

## File Structure

```
src/BioChain.App/src/pages/
  ConstellationPage.tsx          ← grid layout + orchestration
  constellation/
    ViewPanel.tsx                ← shared wrapper with title + resize
    MotifGalaxy.tsx             ← View 1 (Sigma.js graph, extracted from current ConstellationPage)
    RegionalCircuitMap.tsx      ← View 2 (SVG)
    GateBoard.tsx               ← View 3 (React cards)
    PhaseCorridor.tsx           ← View 4 (CSS Grid)
    BindDashboard.tsx           ← View 5 (React + SVG bars)
    ExplanationCore.tsx         ← View 6 (React narrative)
    shared/
      NodeChip.tsx              ← reusable signal pill
      StatusBadge.tsx           ← community status badge
      SeverityBadge.tsx         ← architecture severity badge
      FragilityHalo.tsx         ← fragility indicator
      FocusHighlight.tsx        ← shared highlight animation
```

---

## Verification Criteria

1. All 6 views render without errors
2. Clicking a node in any view highlights it in all other views
3. Gate Board shows all gates with correct state from `evaluate_gate()`
4. Bind Dashboard shows contribution breakdown for each bind
5. Regional Circuit Map shows signals grouped by region with inter-frame flows
6. Phase Corridor shows signal states across temporal phases
7. Explanation Core adapts content based on focused entity
8. Motif capsules expand/collapse in Motif Galaxy
9. Compensation bridges visible as double-line overlays
10. Fragility halos visible on binds and motifs
11. No TypeScript errors, builds clean
12. Responsive layout stacks on mobile
