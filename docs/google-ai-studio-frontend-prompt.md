# NeuroGateway Frontend — Google AI Studio Development Prompt

## Project Overview

Build a modern React frontend for **NeuroGateway** — a biochemical personality analysis platform. The backend uses 27 specialized AI agents to analyze personality through neurochemistry (neurotransmitters, hormones, peptides). The frontend is **3 apps in one**, sharing a single backend API and auth system, switchable via a top-level tab/mode selector:

1. **NeuroWork** — Work personality profiling & team insights
2. **NeuroJournal** — Personal development, self-journaling & growth tracking
3. **NeuroMatch** — Dating-style biochemical compatibility matching

---

## Tech Stack

- **React 19** + TypeScript (strict mode)
- **React Router 7** — client-side routing
- **TanStack Query v5** — data fetching & caching (30s staleTime)
- **Tailwind CSS 4** — utility-first styling, dark theme default
- **Vite** — dev server + build tool
- **Auth**: Keycloak OIDC via `react-oidc-context` + `oidc-client-ts`
  - Authority: `http://localhost:8080/realms/neurogateway`
  - Client ID: `neurogateway-spa` (public SPA client)
  - Scope: `openid profile email`
  - Token: JWT Bearer in `Authorization` header for all protected API calls
- **Charts**: Highcharts (radar, heatmap, sankey, network, sparklines, gauges, timeline)
- **API**: REST at `http://localhost:13370` (proxied via Vite `/api` → backend)

---

## Complete Backend API Reference

**Base URL**: `/api` (proxied to backend)

### Authentication
All endpoints except questionnaire public routes require `Authorization: Bearer <jwt_token>`.
Returns `401` if unauthenticated, `403` if insufficient role.

**Roles**: `admin`, `work`, `private`, `both`, `worker`

### Auth API (`/api/auth`) — Auth Required
| Method | Path | Description | Request / Response |
|--------|------|-------------|-------------------|
| GET | `/api/auth/me` | Current user info + roles | → `{ userId, email, roles: string[], hasSelectedRole: bool }` |
| POST | `/api/auth/set-role` | User sets own role | body: `{ role: string }` → `{ role }` |
| POST | `/api/auth/sync-roles` | Sync IdP claims to DB (first login) | → `200 OK` |
| POST | `/api/auth/resolve-shares` | Resolve pending shares by email | → `200 OK` |
| GET | `/api/auth/admin/users` | List all users (admin only) | → `{ users: [{ userId, email, roles, updatedAt }] }` |
| POST | `/api/auth/admin/set-roles` | Admin sets roles for any user | body: `{ userId, email?, roles: string[] }` → `{ userId, roles }` |

### Persons API (`/api/persons`) — Auth Required
| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/persons` | List all persons for current user | `{ persons: string[] }` |
| POST | `/api/persons` | Create new person | body: `{ name: string }` → `{ personId: uuid, personalityId: int }` |
| GET | `/api/persons/{name}/profile` | Full profile with chemical counts | `{ person, communicationStyle, chemicalCounts: [{chemical, count}], profiles: [{chemical, reasoning}] }` |
| GET | `/api/persons/{name}/dimensions?mode=work\|private` | Dimension scores (24 dimensions) | `{ person, mode, behavioral: DimensionScore[], personal: DimensionScore[] }` |
| GET | `/api/persons/{name}/profile/timeline` | Chemical timeline entries | `{ person, entries: [{chemical, intensityFactor, createdAt}] }` |
| GET | `/api/persons/{name}/shadow-matrix?mode=work\|private` | Chemical x Dimension matrix | `{ person, mode, cells: [{dimension, chemical, shadowLevel, confidence}], dimensions[], chemicals[] }` |
| GET | `/api/persons/{name}/style` | Communication style summary | `{ person, communicationStyle }` |
| GET | `/api/persons/{name}/chemicals` | Chemical observation counts | `{ person, chemicals: [{chemical, count}] }` |
| POST | `/api/persons/{name}/share` | Share person with email | body: `{ email }` → `{ shared: true }` |
| DELETE | `/api/persons/{name}/share?email=x` | Revoke share | `{ unshared: true }` |
| GET | `/api/persons/{name}/shares` | List shares | `{ shares: [{email, sharedAt}] }` |

**DimensionScore shape:**
```ts
{
  name: string;           // e.g. "Ambition", "Emotional Depth"
  section: "work" | "private";
  category: string;       // e.g. "drive", "emotional_landscape"
  score: number;          // 0-100
  confidence: number;     // 0-1
  consistency: number;    // 0-1
  evidenceCount: number;
  evidence: Array<{
    chemical: string;
    layer: "neurotransmitter" | "hormone" | "peptide";
    reasoning: string;
    level: number;
    recency: number;
  }>;
  trajectory?: {
    slope: number;
    direction: "increasing" | "decreasing" | "stable";
    r2: number;
    dataPoints: number;
  };
  circuit?: {
    coherenceScore: number;
    edges: Array<{ chemicalA: string; chemicalB: string; correlation: number; relationship: string; knownMechanism?: string }>;
    pattern: string;
  };
}
```

### Analysis API (`/api/analyze`) — Auth Required
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/analyze/chat` | Full 4-step analysis (agents + reasoning + layer + synthesis) |
| POST | `/api/analyze/work` | Work/CV analysis (agents + reasoning) |
| POST | `/api/analyze/journal` | Journal analysis (agents + reasoning, self-relationship) |
| POST | `/api/analyze/orchestrator` | Multi-turn chat about a person's profile |

**Chat Analysis Request:**
```ts
{ person: string, text: string, relationship?: string, projectedRelationship?: string, save?: boolean }
```
**Chat Analysis Response:**
```ts
{
  person: string;
  sourceType: "chat";
  decisions: Array<{ chemical: string; reasoning: string }>;
  synthesis: string;
  layerResponses: { neurotransmitter: string; hormone: string; peptide: string };
  suggestedResponse: string;
}
```

**Work/Journal Analysis** — same request shape (minus relationship fields for journal), response has `decisions`, `synthesis` but no `layerResponses` or `suggestedResponse`.

**Orchestrator Chat Request:**
```ts
{ person: string, messages: Array<{ role: "user"|"assistant"|"system", content: string }> }
```
Response: `{ response: string }`

### Insights API (`/api/insights`) — Auth Required

This is the richest API for the frontend — provides all dashboard, forecasting, health, and AI-generated content.

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/insights/{person}/dashboard` | Full dashboard bundle | `DashboardResult` (see below) |
| GET | `/api/insights/{person}/forecast` | Chemical trend forecasting | `PersonalForecast` |
| GET | `/api/insights/{person}/prescriptions` | Exercise prescriptions for deficits | `Prescription[]` |
| GET | `/api/insights/{person}/health` | Burnout risk, growth window, overtraining | `HealthIndicators` |
| GET | `/api/insights/{person}/trajectory?period=90` | Historical chemical trajectory | `TrajectoryResult` |
| GET | `/api/insights/{person}/key-chemicals` | Top 3-5 most significant chemicals | `KeyChemicalsResult` |
| GET | `/api/insights/{person}/strengths-challenges` | AI-generated strengths & challenges | `StrengthsChallengesResult` |
| GET | `/api/insights/{person}/cross-profile` | Strength x Challenge interactions | `CrossProfileResult` |
| GET | `/api/insights/{person}/personality-narrative` | AI narrative: MBTI + Big Five + biochemistry | `PersonalityNarrative` |
| POST | `/api/insights/{person}/checkin` | Quick mood check-in → 27 agents | body: `{ text: string }` → `{ analysisTriggered, wordCount, status }` |

**Dashboard Response (all-in-one):**
```ts
{
  profile: {
    person: string;
    maturity: number;           // 0-1 how complete the profile is
    totalObservations: number;
    uniqueChemicals: number;
    levels: Array<{ chemical: string; layer: string; level: number; observationCount: number; variance: number }>;
    topFive: Array<{ chemical: string; layer: string; level: number; observationCount: number; variance: number }>;
  };
  forecast: {
    chemicals: Array<{
      chemical: string;
      trend: "Stable" | "Rising" | "Declining" | "Oscillating" | "AtRisk";
      currentLevel: number;
      projectedLevel: number;
      velocity: number;
      approachingOptimal: boolean;
      driftingFromOptimal: boolean;
      riskNote?: string;
    }>;
    activeCascades: Array<{
      triggerChemical: string;
      affectedChemicals: string[];
      mechanism: string;
      severity: "high" | "medium" | "low";
    }>;
    stableFoundation: string[];
    inFlux: string[];
    overallTrajectory: string;
    narrative: string;
  };
  prescriptions: Array<{
    modality: string;           // e.g. "Strength Training", "Meditation", "Cardio"
    rationale: string;
    targetChemicals: string[];
    priority: number;           // higher = more urgent
  }>;
  health: {
    burnoutRisk: boolean;
    burnoutRatio?: number;      // cortisol:DHEA ratio
    burnoutNote?: string;
    growthWindowOpen: boolean;   // BDNF-based
    growthNote?: string;
    overtrainingIndicator?: string;
    overtrainingRecommendation?: string;
  };
}
```

**Key Chemicals Response:**
```ts
{
  person: string;
  chemicals: Array<{
    chemical: string;
    label: string;
    layer: string;
    layerColor: string;
    level: number;
    levelLabel: string;         // "elevated", "optimal", "depleted", etc.
    optimalCenter: number;
    optimalLow: number;
    optimalHigh: number;
    significance: string;       // "strength", "challenge", "balanced"
    significanceIcon: string;   // emoji
    importance: number;
    observationCount: number;
  }>;
  narrative: string;
}
```

**Strengths & Challenges Response:**
```ts
{
  person: string;
  strengths: StrengthChallengeItem[];
  challenges: StrengthChallengeItem[];
  summary: string;              // AI-generated summary
  generatedAt: string;
}

// Each item:
{
  type: "strength" | "challenge";
  indicator: string;
  title: string;
  chemicalKey: string;
  label: string;
  layer: string;
  layerColor: string;
  level: number;
  optimalCenter: number;
  deviation: number;
  levelLabel: string;
  explanation: string;          // AI-generated
  practicalAdvice: string;      // AI-generated
  brainExercise: string;        // AI-generated
  relatedChemicals: string[];
  relatedLabels: string[];
}
```

**Cross-Profile Response:**
```ts
{
  person: string;
  interactions: Array<{
    strengthChemical: string;
    strengthLabel: string;
    challengeChemical: string;
    challengeLabel: string;
    similarity: number;
    affects: string;
    interaction: string;
    suggestion: string;         // AI-generated
    mechanism: string;
  }>;
  narrative: string;            // AI-generated
  generatedAt: string;
}
```

**Personality Narrative Response:**
```ts
{
  person: string;
  mbtiSummary: string;
  bigFiveSummary: string;
  typeChemistry: string;
  overallPattern: string;
  mbtiInsight?: {
    cognitiveStack: string;
    strengthsNarrative: string;
    blindSpots: string;
    growthPath: string;
    dominantChemicals: string[];
  };
  traitDrivers: Array<{
    trait: string;
    label: string;
    score: number;
    narrative: string;
    pattern: string;
    keyChemicals: string[];
  }>;
  generatedAt: string;
}
```

### MBTI API — Auth Required
| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/mbti/{person}` | MBTI type via embedding similarity | `{ person, typeCode, typeLabel, rankedTypes: [{typeCode, typeLabel, similarity}], note }` |

### Big Five API — Auth Required
| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/bigfive/{person}` | OCEAN traits via embedding similarity | `{ person, traits: [{trait, label, score, highSim, lowSim}], note }` |

### Master Data APIs — Auth Required
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/chemicals` | List all 27 chemicals with key, label, layer |
| GET | `/api/chemicals/{key}` | Get single chemical by key |
| POST | `/api/chemicals` | Create chemical (admin) |
| PUT | `/api/chemicals/{id}` | Update chemical (admin) |
| DELETE | `/api/chemicals/{id}` | Delete chemical (admin) |
| GET | `/api/dimensions` | List all 24 dimensions with chemical affinities |
| POST | `/api/dimensions` | Create dimension (admin) |
| PUT | `/api/dimensions/{id}` | Update dimension (admin) |
| DELETE | `/api/dimensions/{id}` | Delete dimension (admin) |
| PUT | `/api/dimensions/{id}/affinities` | Set chemical affinity weight (admin) |
| DELETE | `/api/dimensions/{dimensionId}/affinities/{chemicalId}` | Remove affinity (admin) |
| GET | `/api/chemical-interactions` | List all chemical interaction pairs |
| GET | `/api/chemical-interactions/{chemical}` | Get interactions for one chemical |
| POST | `/api/chemical-interactions` | Create interaction (admin) |
| PUT | `/api/chemical-interactions/{id}` | Update interaction (admin) |
| DELETE | `/api/chemical-interactions/{id}` | Delete interaction (admin) |
| GET | `/api/relationships` | List 10 relationship types |

### Embeddings API — Auth Required (Admin)
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/embeddings/backfill` | Generate missing embedding vectors |

### Questionnaire API (`/api/questionnaire`) — Mixed Auth
| Method | Path | Auth | Description | Response |
|--------|------|------|-------------|----------|
| GET | `/api/questionnaire/questions` | Public | Get 18 questions with options | `{ questions: [{sortOrder, scenario, isInverted, options: [{id, label, text}]}] }` |
| POST | `/api/questionnaire` | Required | Create questionnaire for a person | body: `{ personName }` → `{ token }` |
| GET | `/api/questionnaire/{token}` | Public | Load questionnaire by share token | Questionnaire view with person + status + questions |
| POST | `/api/questionnaire/{token}/submit` | Public | Submit all 18 selected option IDs | body: `{ selectedItemIds: int[] }` → `{ status: "completed" }` |
| POST | `/api/questionnaire/{token}/answer` | Public | Submit single answer + run targeted agents | body: `{ itemId: int }` → `{ answeredCount, totalQuestions, isComplete, chemicalsAnalyzed }` |

### Health / Diagnostic Endpoints
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/ping` | None | Health check → `"pong"` |
| GET | `/health` | None | ASP.NET health → `"Healthy"` |

---

## Domain Model

### 27 Biochemicals (3 layers)

**Neurotransmitters (7):** dopamine, serotonin, norepinephrine, gaba, acetylcholine, endocannabinoid, glutamate

**Hormones (10):** cortisol, testosterone, estradiol, progesterone, thyroid, adrenaline, melatonin, dhea, prolactin, oxytocin_h

**Peptides (10):** oxytocin, vasopressin, endorphins, enkephalins, dynorphin, substance_p, crh, npy, bdnf, orexin

### 24 Dimensions (scored 0-100)

**Work Section (12):**
- Drive & Trajectory: Ambition, Risk Tolerance, Persistence
- Leadership: Team Orientation, Strategic Thinking, Stress Capacity
- Execution: Competitive Drive, Context Switching, Problem Solving
- Professional Growth: Knowledge Transfer, Work-Life Balance, Career Resilience

**Private Section (12):**
- Emotional Landscape: Emotional Depth, Emotional Regulation, Sensitivity
- Relational Style: Attachment Security, Intimacy Capacity, Social Energy
- Inner Drive: Self-Awareness, Playfulness, Purpose & Meaning
- Resilience & Recovery: Stress Response, Healing Capacity, Inner Peace

### 10 Relationship Types
partner, dating, ex, family, friend, coworker, mentor, acquaintance, coparent, therapist

### NeuroTriangulate-18 Questionnaire
18 scenario-based questions, each with 3-4 options mapped to primary/secondary chemicals. Supports both batch submit (all 18 at once) and progressive single-answer mode with real-time targeted agent analysis per answer.

Example:
- "When you first wake up, what pulls you out of bed?"
  - A: Curiosity (dopamine + orexin)
  - B: Routine (serotonin + melatonin)
  - C: People (oxytocin_h + vasopressin)

---

## App 1: NeuroWork — Work Personality Profiling

### Purpose
Analyze team members' work personalities from CVs, meeting transcripts, emails, and work conversations. Help managers understand team chemistry and optimize collaboration.

### Pages

**1. Dashboard** (`/work`)
- Grid of person cards showing name, top 3 chemicals, overall "work profile archetype"
- Quick-add person button
- Filter/search
- Each card shows a mini radar chart of the 12 work dimensions
- Health indicator badges (burnout risk, growth window) from `/api/insights/{person}/health`

**2. Person Detail** (`/work/persons/:name`)
- Header: Name, communication style, MBTI type badge, creation date
- Tab layout:
  - **Overview**: Radar chart (Highcharts polar) of 12 work dimensions + archetype label + personality narrative snippet
  - **Chemicals**: Bar chart of chemical observation counts by layer + key chemicals panel from `/api/insights/{name}/key-chemicals`
  - **Dimensions**: Detailed dimension cards with score, confidence, evidence accordion, trajectory arrows, circuit coherence indicator
  - **Timeline**: Multi-series line chart of chemical activity over time from `/api/persons/{name}/profile/timeline`
  - **Shadow Matrix**: Heatmap (Highcharts) of dimensions x chemicals from `/api/persons/{name}/shadow-matrix`
  - **Interactions**: Network graph of chemical interactions from `/api/chemical-interactions`
  - **Insights**: Dashboard view from `/api/insights/{name}/dashboard` — forecast trends, health indicators, prescriptions, strengths/challenges
  - **Personality**: Full narrative from `/api/insights/{name}/personality-narrative` + MBTI from `/api/mbti/{name}` + Big Five from `/api/bigfive/{name}`
- Actions: Share, Analyze text, Generate questionnaire link, Quick check-in

**3. Analyze** (`/work/analyze`)
- Mode selector: Work (CV/LinkedIn), Chat (meeting notes, emails)
- Person dropdown (select existing or create new)
- Text input area (large, supports paste)
- Optional relationship context dropdown (from `/api/relationships`)
- Results panel: decisions list, synthesis narrative, chemical badges
- After analysis, show updated dimension scores

**4. Team Compare** (`/work/compare`)
- Select 2-4 persons to compare side-by-side
- Overlaid radar charts of work dimensions
- Compatibility matrix: which dimension gaps complement each other
- Communication style comparison
- Chemical forecast comparison — are team members' trajectories converging or diverging?
- Team chemistry suggestions

**5. Settings** (`/work/settings`)
- Master data CRUD: dimensions, chemicals, interactions, affinities (admin only) — uses all the POST/PUT/DELETE master data endpoints
- Manage shares
- User role management (admin only) from `/api/auth/admin/*`

---

## App 2: NeuroJournal — Personal Development & Journaling

### Purpose
A private journaling app where users track their emotional and biochemical patterns over time. Write journal entries that get analyzed automatically. Track personal growth, emotional regulation, and self-awareness.

### Pages

**1. Journal Feed** (`/journal`)
- Reverse-chronological feed of journal entries
- Each entry shows: date, text snippet, chemical badges (top 3 detected), mood indicator
- Floating "+" button to create new entry
- Quick check-in button → `/api/insights/{person}/checkin`
- Filter by date range, chemical, dimension

**2. Write Entry** (`/journal/write`)
- Clean, distraction-free writing interface (like a note app)
- Person is always "self" (the logged-in user's own profile)
- After saving, shows real-time analysis results:
  - Chemical decisions with reasoning
  - Synthesis paragraph
  - Mood/energy indicators derived from chemicals
- Relationship defaults to "self", sourceType = "journal"

**3. My Profile** (`/journal/profile`)
- The user's own biochemical personality profile
- 12 private dimensions radar chart
- Key chemicals panel with optimal range gauges from `/api/insights/{person}/key-chemicals`
- Strengths & challenges cards from `/api/insights/{person}/strengths-challenges` — each with explanation, practical advice, brain exercises
- Cross-profile analysis from `/api/insights/{person}/cross-profile` — how your strengths compensate for your challenges
- Emotional landscape breakdown
- Growth trajectory charts (how dimensions changed over time)
- Archetype label: "The Empathic Strategist", "The Resilient Explorer", etc.

**4. Insights Dashboard** (`/journal/insights`)
- **Full dashboard** from `/api/insights/{person}/dashboard`:
  - Profile maturity meter
  - Chemical level gauges (current vs optimal range)
  - Forecast panel: per-chemical trend arrows (Rising/Declining/Stable/AtRisk/Oscillating)
  - Cascade alerts: warnings when chemical shifts trigger chain reactions
  - Health indicators: burnout risk gauge, growth window indicator, overtraining warning
  - Exercise prescriptions: modality cards with priority ranking and target chemicals
- Chemical trajectory chart from `/api/insights/{person}/trajectory?period=90` — historical line chart over 90 days
- Dimension trends: sparklines for each private dimension over weeks/months
- Mood calendar (heatmap of dominant chemicals per day)
- Personality narrative from `/api/insights/{person}/personality-narrative`

**5. Questionnaire** (`/journal/questionnaire`)
- NeuroTriangulate-18 self-assessment
- Beautiful, one-question-at-a-time flow (like Typeform)
- Uses progressive single-answer mode: `POST /api/questionnaire/{token}/answer` per question
- Shows real-time progress (answeredCount/18) and which chemicals were just analyzed
- Progress bar, scenario descriptions
- Results integrated into profile

**6. Chat with Your Profile** (`/journal/chat`)
- Orchestrator chat endpoint — ask questions about your own profile
- "What are my emotional patterns?", "How has my stress changed?"
- Conversational AI with full profile context via `/api/analyze/orchestrator`

---

## App 3: NeuroMatch — Biochemical Dating & Compatibility

### Purpose
A dating-app-style experience where users compare their biochemical profiles to find compatible matches. Uses the private dimension scores, chemical profiles, and interaction data to compute compatibility.

### Design Language
Think **Hinge/Bumble meets neuroscience**. Warm, inviting, card-based UI. Purple/teal gradient accents. Profile photos optional (the "profile" is your biochemical signature).

### Pages

**1. Discover** (`/match`)
- **Swipe-style cards** showing other users' biochemical profiles (shared via the sharing system)
- Each card shows:
  - Name, archetype label ("The Nurturing Innovator")
  - Mini radar chart of their 12 private dimensions
  - Top 3 dominant chemicals with icons and short labels (from their key-chemicals)
  - Compatibility score (computed client-side from dimension/chemical overlap)
  - A "vibe quote" from their communication style
- Swipe right = interested, left = pass, up = super-match
- Compatibility algorithm (see Shared Components below)

**2. My Profile** (`/match/profile`)
- Your biochemical dating profile
- Shows your 12 private dimensions as a visual signature (radar chart)
- MBTI type badge + Big Five bar chart
- Dominant chemical "personality cocktail" visualization
- Key chemicals from `/api/insights/{person}/key-chemicals`
- Archetype label with description
- Communication style
- "What I bring" — top 3 strengths from `/api/insights/{person}/strengths-challenges`
- "What I seek" — top 3 challenges (seeking complement)
- Personality narrative snippet from `/api/insights/{person}/personality-narrative`

**3. Matches** (`/match/matches`)
- List of mutual matches
- Each match shows:
  - Compatibility score breakdown (synergy, complementary, shared values)
  - Overlaid radar charts (your dimensions vs. theirs)
  - Chemical interaction diagram (how your dominant chemicals interact with theirs)
  - Conversation starters based on shared or complementary traits

**4. Compare** (`/match/compare/:name`)
- Deep-dive comparison between you and a match
- Split-screen dimension comparison
- Chemical interaction network (merged graph)
- Synergy highlights: "Your high dopamine pairs well with their high serotonin — you bring drive, they bring stability"
- Shadow matrix overlay: where your strengths cover their gaps
- Trajectory comparison from forecast data: are you both growing in compatible directions?
- Cross-profile interaction suggestions

**5. Questionnaire** (`/match/questionnaire`)
- Same NeuroTriangulate-18 flow as Journal app
- Framed as "Build your biochemical dating profile"
- Required before accessing Discover

**6. Chemistry Report** (`/match/chemistry/:name`)
- PDF-exportable compatibility report
- Sections: Overall compatibility, Dimension alignment, Chemical synergy, Health compatibility, Growth potential, Communication tips
- Shareable link

---

## Shared Components

### Navigation
- Top-level mode switcher: **Work** | **Journal** | **Match** (3 icons/tabs)
- Each mode has its own sidebar/bottom nav
- User avatar + role badge + logout in top-right
- Role selection page on first login (if `hasSelectedRole` is false from `/api/auth/me`)
- Call `/api/auth/sync-roles` and `/api/auth/resolve-shares` on login

### Auth Flow
1. User visits app → redirected to Keycloak login
2. After OIDC callback → store token in session
3. Call `POST /api/auth/sync-roles` to sync IdP claims
4. Call `POST /api/auth/resolve-shares` to match email with pending shares
5. Call `GET /api/auth/me` — if `hasSelectedRole` is false, show role selection page using `POST /api/auth/set-role`
6. Route to last-used mode or Dashboard

### Person Management
- In **Work mode**: persons are other people (team members, clients)
- In **Journal mode**: "person" is always the logged-in user (auto-created on first login)
- In **Match mode**: "person" is the logged-in user + shared persons from other users are potential matches

### Compatibility Algorithm (Client-Side)
```ts
function computeCompatibility(myDims: DimensionScore[], theirDims: DimensionScore[]): CompatibilityResult {
  // 1. Shared Values (30% weight) — similar scores on key dimensions
  const sharedDims = ["Self-Awareness", "Purpose & Meaning", "Emotional Depth"];
  const sharedScore = average(sharedDims.map(d =>
    100 - Math.abs(findDim(myDims, d).score - findDim(theirDims, d).score)
  ));

  // 2. Complementary (40% weight) — one high where other is lower
  const complementPairs = [
    ["Social Energy", "Inner Peace"],
    ["Emotional Depth", "Emotional Regulation"],
    ["Playfulness", "Persistence"],
  ];
  const complementScore = average(complementPairs.map(([a, b]) => {
    const myA = findDim(myDims, a).score, theirB = findDim(theirDims, b).score;
    const myB = findDim(myDims, b).score, theirA = findDim(theirDims, a).score;
    return (Math.min(myA, theirB) + Math.min(myB, theirA)) / 2;
  }));

  // 3. Chemical Synergy (30% weight) — from interaction modFactors
  // Uses /api/chemical-interactions to find positive interactions between dominant chemicals

  return { overall: 0.3 * sharedScore + 0.4 * complementScore + 0.3 * synergyScore };
}
```

### Chemical Visualization Palette
Each chemical has a consistent color across all 3 apps:
- Dopamine: electric blue (#3b82f6)
- Serotonin: warm amber (#f59e0b)
- Norepinephrine: red (#ef4444)
- GABA: green (#22c55e)
- Oxytocin: pink (#ec4899)
- Cortisol: gray-red (#94a3b8)
- Testosterone: orange (#f97316)
- Endorphins: purple (#a855f7)
- BDNF: teal (#14b8a6)
- Acetylcholine: cyan (#06b6d4)
- (assign distinct colors for all 27)

### Highcharts Visualization Guide

Use Highcharts for all data visualization. Here's which chart type to use where:

**Radar / Polar Chart** — Dimension visualization
- 12-axis polar chart for work or private dimensions
- Score as area fill, confidence as opacity
- Overlay mode for comparing 2+ people

**Heatmap** — Shadow matrix
- Rows: dimensions, Columns: chemicals
- Color scale: gray (level 1) → deep purple (level 5)
- Tooltip shows shadow level text + confidence

**Line / Spline Chart** — Timelines & trajectories
- Chemical trajectory over time (multi-series, one per chemical)
- Forecast projection as dashed extension
- Use `/api/insights/{person}/trajectory` data

**Bar / Column Chart** — Chemical observations & levels
- Grouped by layer (neurotransmitter / hormone / peptide)
- Chemical level vs optimal range as bullet chart or range plot

**Gauge / Solid Gauge** — Health indicators
- Burnout ratio gauge (green → yellow → red)
- Profile maturity percentage
- Individual chemical level vs optimal

**Network Graph** — Chemical interactions
- Nodes = chemicals (colored by layer)
- Edges = interactions (thickness = modFactor, color = positive/negative)
- Highlight active cascades

**Sankey Diagram** — Dimension → Chemical flow
- Show which chemicals drive which dimensions
- Width = affinity weight

**Sparklines** — Trend indicators
- Small inline charts for chemical trends in list views
- Used in dashboard cards and forecast panels

**Pie / Donut** — Layer distribution
- Proportion of observations per layer (neurotransmitter vs hormone vs peptide)

### Shared UI Patterns
- **Radar charts** for dimension visualization (Highcharts polar)
- **Chemical badges**: small pills with chemical color + icon
- **Archetype labels**: computed from top 3 chemicals + dimension pattern
- **Score rings**: circular progress for 0-100 scores with confidence ring
- **Evidence accordions**: expandable reasoning text per decision
- **Trajectory arrows**: up/down/stable indicators on dimensions with slope value
- **Trend badges**: "Rising", "Declining", "Stable", "AtRisk", "Oscillating" with color coding
- **Cascade alert cards**: trigger chemical → affected chemicals with severity indicator
- **Prescription cards**: modality icon + rationale + target chemical badges + priority
- **Health indicator widgets**: burnout gauge, growth window badge, overtraining alert
- **MBTI type badge**: 4-letter code with label (e.g., "INTJ — The Architect")
- **Big Five bars**: 5 horizontal bars (O, C, E, A, N) with score and high/low labels
- **Strength/Challenge cards**: icon + title + explanation + practical advice + brain exercise

---

## Design Guidelines

- **Dark mode first** — `bg-gray-950` base, `gray-900` cards, `gray-800` borders
- **Accent gradients**: indigo→purple for Work, emerald→teal for Journal, pink→purple for Match
- **Typography**: Inter or system font stack, clear hierarchy
- **Cards**: rounded-2xl with subtle border, hover elevation
- **Animations**: Framer Motion for page transitions, card swipes, chart reveals
- **Mobile-first**: responsive grid, bottom nav on mobile, sidebar on desktop
- **Accessibility**: ARIA labels, keyboard navigation, focus rings
- **Loading states**: Skeleton loaders for cards and charts, shimmer effect
- **Empty states**: Helpful prompts when no data yet ("Analyze your first conversation to see results")

---

## File Structure

```
src/
├── api/
│   ├── client.ts              # Fetch wrapper with auth token
│   └── types.ts               # All API response TypeScript types
├── auth/
│   ├── AuthProvider.tsx        # Keycloak OIDC setup
│   ├── RequireAuth.tsx         # Protected route wrapper
│   └── RoleGate.tsx            # Role-based component visibility
├── components/
│   ├── shared/                 # Reusable across all 3 apps
│   │   ├── charts/
│   │   │   ├── RadarChart.tsx          # Highcharts polar for dimensions
│   │   │   ├── HeatmapChart.tsx        # Shadow matrix heatmap
│   │   │   ├── TimelineChart.tsx       # Chemical timeline line chart
│   │   │   ├── TrajectoryChart.tsx     # Forecast trajectory with projections
│   │   │   ├── NetworkGraph.tsx        # Chemical interaction network
│   │   │   ├── SankeyDiagram.tsx       # Dimension → Chemical flow
│   │   │   ├── GaugeChart.tsx          # Health indicator gauges
│   │   │   ├── SparklineChart.tsx      # Inline trend sparklines
│   │   │   └── BigFiveBars.tsx         # OCEAN horizontal bar chart
│   │   ├── ChemicalBadge.tsx
│   │   ├── DimensionCard.tsx
│   │   ├── ScoreRing.tsx
│   │   ├── ArchetypeLabel.tsx
│   │   ├── TrajectoryArrow.tsx
│   │   ├── TrendBadge.tsx
│   │   ├── MbtiTypeBadge.tsx
│   │   ├── CascadeAlertCard.tsx
│   │   ├── PrescriptionCard.tsx
│   │   ├── HealthIndicatorWidget.tsx
│   │   ├── StrengthChallengeCard.tsx
│   │   ├── KeyChemicalGauge.tsx
│   │   └── PersonalityNarrativePanel.tsx
│   ├── work/                   # NeuroWork components
│   ├── journal/                # NeuroJournal components
│   └── match/                  # NeuroMatch components
├── hooks/
│   ├── usePersons.ts
│   ├── useDimensions.ts
│   ├── useChemicals.ts
│   ├── useAnalyze.ts
│   ├── useInsights.ts          # dashboard, forecast, health, prescriptions, trajectory
│   ├── useKeyChemicals.ts
│   ├── useStrengthsChallenges.ts
│   ├── useCrossProfile.ts
│   ├── usePersonalityNarrative.ts
│   ├── useMbti.ts
│   ├── useBigFive.ts
│   ├── useCompatibility.ts
│   ├── useQuestionnaire.ts
│   ├── useAuth.ts              # me, set-role, sync-roles
│   └── useCheckIn.ts
├── pages/
│   ├── RoleSelectionPage.tsx   # First-login role picker
│   ├── work/
│   │   ├── WorkDashboard.tsx
│   │   ├── WorkPersonDetail.tsx
│   │   ├── WorkAnalyze.tsx
│   │   ├── WorkCompare.tsx
│   │   └── WorkSettings.tsx
│   ├── journal/
│   │   ├── JournalFeed.tsx
│   │   ├── JournalWrite.tsx
│   │   ├── JournalProfile.tsx
│   │   ├── JournalInsights.tsx
│   │   ├── JournalChat.tsx
│   │   └── JournalQuestionnaire.tsx
│   ├── match/
│   │   ├── MatchDiscover.tsx
│   │   ├── MatchProfile.tsx
│   │   ├── MatchList.tsx
│   │   ├── MatchCompare.tsx
│   │   ├── MatchChemistryReport.tsx
│   │   └── MatchQuestionnaire.tsx
│   └── LoginPage.tsx
├── utils/
│   ├── compatibility.ts        # Compatibility algorithm
│   ├── archetypes.ts          # Archetype computation
│   └── colors.ts              # Chemical color palette (all 27)
├── types/
│   └── api.ts                 # TypeScript API types
├── App.tsx                    # Router with mode switching
├── main.tsx
└── index.css                  # Tailwind imports + Highcharts theme
```

---

## Getting Started

The backend is already running at `http://localhost:13370`. Keycloak is at `http://localhost:8080`.

1. Default login: `admin` / `admin` (Keycloak neurogateway realm)
2. After login, call `POST /api/auth/sync-roles` then `POST /api/auth/resolve-shares`
3. Check `GET /api/auth/me` — if no role selected, show role selection page
4. Create a person (yourself) or use existing data
5. All analysis endpoints accept free text and return chemical decisions + synthesis
6. Dimension scores are computed server-side from accumulated chemical evidence
7. The questionnaire supports both batch (submit all 18) and progressive (one at a time) modes
8. The insights API is the richest source — use `/dashboard` for the all-in-one view
9. MBTI and Big Five classifications are available per person
10. Chemical trajectories forecast 90 days ahead with cascade alerts
