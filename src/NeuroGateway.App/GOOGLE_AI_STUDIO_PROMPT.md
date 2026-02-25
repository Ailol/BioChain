# NeuroReact Frontend — Complete Generation Prompt

You are building a production React frontend called **NeuroReact** for the **NeuroGateway** biochemical personality analysis backend. Generate a COMPLETE, working application with all files, all pages, all charts, and all API integration.

---

## TECH STACK (mandatory)

```
React 19 + TypeScript + Vite
TailwindCSS 4 (utility-first, dark theme default)
React Router v7 (file-based routing)
Highcharts + highcharts-react-official (ALL charts)
Axios (HTTP client with interceptors)
Zustand (state management)
keycloak-js (auth adapter)
```

### package.json dependencies
```json
{
  "dependencies": {
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "react-router": "^7.0.0",
    "axios": "^1.7.0",
    "zustand": "^5.0.0",
    "highcharts": "^12.0.0",
    "highcharts-react-official": "^3.2.0",
    "keycloak-js": "^26.0.0",
    "clsx": "^2.1.0",
    "lucide-react": "^0.400.0"
  },
  "devDependencies": {
    "@vitejs/plugin-react": "^4.3.0",
    "typescript": "^5.6.0",
    "tailwindcss": "^4.0.0",
    "@tailwindcss/vite": "^4.0.0",
    "vite": "^6.0.0",
    "@types/react": "^19.0.0",
    "@types/react-dom": "^19.0.0"
  }
}
```

---

## PROJECT STRUCTURE

```
neuroreact/
├── index.html
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.ts
├── src/
│   ├── main.tsx
│   ├── App.tsx
│   ├── index.css                    # Tailwind imports + dark theme vars
│   ├── api/
│   │   ├── client.ts                # Axios instance with Keycloak token injection
│   │   ├── auth.ts                  # Auth API calls
│   │   ├── persons.ts               # Person CRUD + profile + dimensions
│   │   ├── analyze.ts               # Chat/work/journal analysis
│   │   ├── insights.ts              # Dashboard, forecast, health, etc.
│   │   ├── classification.ts        # MBTI + Big Five
│   │   ├── questionnaire.ts         # Questionnaire CRUD
│   │   ├── chemicals.ts             # Chemical CRUD (admin)
│   │   ├── interactions.ts          # Chemical interactions CRUD (admin)
│   │   ├── dimensions.ts            # Dimension CRUD (admin)
│   │   └── embeddings.ts            # Embedding admin
│   ├── auth/
│   │   ├── keycloak.ts              # Keycloak init + config
│   │   ├── AuthProvider.tsx         # React context with Keycloak
│   │   └── ProtectedRoute.tsx       # Role-gated route wrapper
│   ├── stores/
│   │   ├── authStore.ts             # User info, roles, selected person
│   │   └── personStore.ts           # Active person, person list cache
│   ├── types/
│   │   └── index.ts                 # ALL TypeScript interfaces
│   ├── charts/
│   │   ├── DimensionRadar.tsx       # Polar/radar for 24 dimensions
│   │   ├── ChemicalLevelsBar.tsx    # Grouped column chart by layer
│   │   ├── ProfileTimeline.tsx      # Spline with markers over time
│   │   ├── BigFiveRadar.tsx         # Polar for 5 OCEAN traits
│   │   ├── MbtiRankings.tsx         # Horizontal bar for 16 types
│   │   ├── ForecastArea.tsx         # Areaspline current→projected
│   │   ├── ShadowHeatmap.tsx        # Heatmap chemical×dimension
│   │   ├── BurnoutGauge.tsx         # Solid gauge 0-100
│   │   ├── TrajectorySparklines.tsx # Mini spline per chemical
│   │   ├── StrengthsChallengesBar.tsx # Diverging bar from optimal
│   │   ├── KeyChemicalsDonut.tsx    # Pie/donut by importance
│   │   ├── CrossProfileNetwork.tsx  # Network graph interactions
│   │   └── CascadeFlow.tsx          # Sankey trigger→affected
│   ├── components/
│   │   ├── Layout.tsx               # Sidebar + topbar + content
│   │   ├── Sidebar.tsx              # Role-based navigation
│   │   ├── PersonSelector.tsx       # Dropdown to pick active person
│   │   ├── DecisionCards.tsx        # Chemical decision display
│   │   ├── SynthesisPanel.tsx       # AI synthesis text display
│   │   ├── LoadingSpinner.tsx
│   │   ├── ErrorBoundary.tsx
│   │   └── EmptyState.tsx
│   ├── pages/
│   │   ├── LoginPage.tsx
│   │   ├── RoleSelectionPage.tsx
│   │   ├── personal/
│   │   │   ├── JournalEntryPage.tsx
│   │   │   ├── JournalHistoryPage.tsx
│   │   │   ├── MyProfilePage.tsx
│   │   │   ├── MyChemistryPage.tsx
│   │   │   ├── InsightsDashboardPage.tsx
│   │   │   ├── PersonalityPage.tsx
│   │   │   └── CheckInPage.tsx
│   │   ├── professional/
│   │   │   ├── AnalyzeDocumentPage.tsx
│   │   │   ├── CandidateListPage.tsx
│   │   │   ├── CandidateProfilePage.tsx
│   │   │   └── ChatAnalysisPage.tsx
│   │   └── admin/
│   │       ├── UserManagementPage.tsx
│   │       ├── ChemicalManagementPage.tsx
│   │       ├── InteractionManagementPage.tsx
│   │       ├── DimensionManagementPage.tsx
│   │       └── EmbeddingAdminPage.tsx
│   └── utils/
│       ├── colors.ts                # Layer colors, chemical colors
│       └── format.ts                # Date formatting, label formatting
```

---

## KEYCLOAK AUTH CONFIGURATION

```typescript
// src/auth/keycloak.ts
import Keycloak from "keycloak-js";

const keycloak = new Keycloak({
  url: "https://YOUR_KEYCLOAK_URL",       // PLACEHOLDER — user fills in
  realm: "neurogateway",                   // realm name
  clientId: "neuroreact",                  // public client ID
});

export default keycloak;
```

### Auth flow:
1. App loads → Keycloak `init({ onLoad: "login-required" })`
2. On success → call `POST /api/auth/sync-roles` (first login syncs IdP claims to DB)
3. Call `GET /api/auth/me` → get `{ userId, email, roles, hasSelectedRole }`
4. If `!hasSelectedRole` → redirect to RoleSelectionPage
5. RoleSelectionPage calls `POST /api/auth/set-role` with chosen role
6. After role set → redirect to appropriate dashboard

### Role-based routing:
- `private` role → Personal pages (journal, profile, insights)
- `work` role → Professional pages (candidates, CV analysis)
- `both` role → Both personal + professional pages
- `admin` role → Admin pages + all other pages

### API client token injection:
```typescript
// src/api/client.ts
import axios from "axios";
import keycloak from "../auth/keycloak";

const api = axios.create({
  baseURL: "http://localhost:13370",    // Backend URL
});

api.interceptors.request.use((config) => {
  if (keycloak.token) {
    config.headers.Authorization = `Bearer ${keycloak.token}`;
  }
  return config;
});

// Auto-refresh token
api.interceptors.response.use(
  (res) => res,
  async (err) => {
    if (err.response?.status === 401) {
      await keycloak.updateToken(30);
      err.config.headers.Authorization = `Bearer ${keycloak.token}`;
      return axios(err.config);
    }
    return Promise.reject(err);
  }
);

export default api;
```

---

## COMPLETE API REFERENCE

### Auth API (`/api/auth`)

| Method | Route | Request Body | Response |
|--------|-------|-------------|----------|
| GET | `/api/auth/me` | — | `{ userId, email, roles[], hasSelectedRole }` |
| POST | `/api/auth/set-role` | `{ role }` | `{ role }` |
| POST | `/api/auth/sync-roles` | — | `{}` |
| POST | `/api/auth/resolve-shares` | — | `{}` |
| GET | `/api/auth/admin/users` | — | `{ users: [{ userId, email, roles[], updatedAt }] }` |
| POST | `/api/auth/admin/set-roles` | `{ userId, email?, roles[] }` | `{ userId, roles[] }` |

### Persons API (`/api/persons`)

| Method | Route | Request | Response |
|--------|-------|---------|----------|
| GET | `/api/persons/` | — | `{ persons: string[] }` |
| POST | `/api/persons/` | `{ name }` | `{ personId, personalityId }` |
| GET | `/api/persons/{name}/profile` | — | `{ person, communicationStyle, chemicalCounts[], profiles[] }` |
| GET | `/api/persons/{name}/style` | — | `{ person, communicationStyle }` |
| GET | `/api/persons/{name}/chemicals` | — | `{ person, chemicals: [{ chemical, count }] }` |
| GET | `/api/persons/{name}/profile/timeline` | — | `{ person, entries: [{ chemical, intensityFactor, createdAt }] }` |
| POST | `/api/persons/{name}/share` | `{ email }` | `{ shared: true }` |
| DELETE | `/api/persons/{name}/share?email=x` | — | `{ unshared: true }` |
| GET | `/api/persons/{name}/shares` | — | `{ shares: [{ email, sharedAt }] }` |
| GET | `/api/persons/{name}/dimensions?mode=work\|private` | — | `{ person, mode, behavioral: DimensionScore[], personal: DimensionScore[] }` |
| GET | `/api/persons/{name}/shadow-matrix?mode=work\|private` | — | `ShadowMatrixResponse` |

### Analyze API (`/api/analyze`)

| Method | Route | Request | Response |
|--------|-------|---------|----------|
| POST | `/api/analyze/chat` | `{ person, text, relationship?, projectedRelationship?, save? }` | `{ person, sourceType, decisions[], synthesis, layerResponses, suggestedResponse }` |
| POST | `/api/analyze/work` | `{ person, text, relationship?, save? }` | `{ person, sourceType, decisionsCount, decisions[], synthesis }` |
| POST | `/api/analyze/journal` | `{ person, text, save? }` | `{ person, sourceType, decisionsCount, decisions[], synthesis }` |
| POST | `/api/analyze/orchestrator` | `{ person, messages: [{ role, content }] }` | `{ response }` |

### Insights API (`/api/insights`)

| Method | Route | Response |
|--------|-------|----------|
| GET | `/api/insights/{person}/dashboard` | `DashboardResultDto` |
| GET | `/api/insights/{person}/forecast` | `PersonalForecastDto` |
| GET | `/api/insights/{person}/prescriptions` | `PrescriptionDto[]` |
| GET | `/api/insights/{person}/health` | `HealthIndicatorsDto` |
| GET | `/api/insights/{person}/trajectory?period=90` | `TrajectoryResultDto` |
| GET | `/api/insights/{person}/key-chemicals` | `KeyChemicalsResultDto` |
| GET | `/api/insights/{person}/strengths-challenges` | `StrengthsChallengesResultDto` |
| GET | `/api/insights/{person}/cross-profile` | `CrossProfileResultDto` |
| GET | `/api/insights/{person}/personality-narrative` | `PersonalityNarrativeDto` |
| POST | `/api/insights/{person}/checkin` | `{ text }` → `CheckInResponse` |

### Classification APIs

| Method | Route | Response |
|--------|-------|----------|
| GET | `/api/mbti/{person}` | `MbtiResultDto` |
| GET | `/api/bigfive/{person}` | `BigFiveResultDto` |

### Questionnaire API (`/api/questionnaire`)

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/questionnaire/questions` | Public | — | `{ questions[] }` |
| POST | `/api/questionnaire/` | Auth | `{ personName }` | `{ token }` |
| GET | `/api/questionnaire/{token}` | Public | — | Questionnaire view |
| POST | `/api/questionnaire/{token}/submit` | Public | `{ selectedItemIds[] }` | `{ status }` |
| POST | `/api/questionnaire/{token}/answer` | Public | `{ itemId }` | Result |

### Chemicals API (`/api/chemicals`) — Admin

| Method | Route | Request | Response |
|--------|-------|---------|----------|
| GET | `/api/chemicals/` | — | `ChemicalDto[]` |
| GET | `/api/chemicals/{key}` | — | `ChemicalDto` |
| POST | `/api/chemicals/` | `{ key, label, layer }` | `ChemicalDto` (201) |
| PUT | `/api/chemicals/{id}` | `{ key, label, layer }` | 204 |
| DELETE | `/api/chemicals/{id}` | — | 204 |

### Chemical Interactions API (`/api/chemical-interactions`) — Admin

| Method | Route | Request | Response |
|--------|-------|---------|----------|
| GET | `/api/chemical-interactions/` | — | `ChemicalInteractionDto[]` |
| GET | `/api/chemical-interactions/{chemical}` | — | `ChemicalInteractionDto[]` |
| POST | `/api/chemical-interactions/` | `{ sourceChemicalId, targetChemicalId, modFactor, mechanism?, notes? }` | `{ id }` (201) |
| PUT | `/api/chemical-interactions/{id}` | `{ modFactor, mechanism?, notes? }` | 204 |
| DELETE | `/api/chemical-interactions/{id}` | — | 204 |

### Dimensions API (`/api/dimensions`) — Admin

| Method | Route | Request | Response |
|--------|-------|---------|----------|
| GET | `/api/dimensions/` | — | `DimensionDto[]` |
| POST | `/api/dimensions/` | `{ name, section, category, description, workRelevance, privateRelevance, sortOrder }` | 201 |
| PUT | `/api/dimensions/{id}` | same as POST | 204 |
| DELETE | `/api/dimensions/{id}` | — | 204 |
| PUT | `/api/dimensions/{id}/affinities` | `{ chemicalId, weight }` | 204 |
| DELETE | `/api/dimensions/{dimId}/affinities/{chemId}` | — | 204 |

### Embeddings API (`/api/embeddings`) — Admin

| Method | Route | Request | Response |
|--------|-------|---------|----------|
| POST | `/api/embeddings/backfill` | `{ person? }` | `{ analyzed_data_embeddings, profile_embeddings, total, message }` |
| POST | `/api/embeddings/reembed-prototypes` | — | `{ mbti_deleted, bigfive_deleted, total, message }` |

### Relationships API (`/api/relationships`)

| Method | Route | Response |
|--------|-------|----------|
| GET | `/api/relationships/` | `{ relationshipTypes: [{ name, description }] }` |

---

## COMPLETE TYPESCRIPT INTERFACES

Put ALL of these in `src/types/index.ts`:

```typescript
// ═══════════════════════════════════════════════
// REQUEST MODELS
// ═══════════════════════════════════════════════

export interface ChatAnalyzeRequest {
  person: string;
  text: string;
  relationship?: string | null;
  projectedRelationship?: string | null;
  save?: boolean;
}

export interface WorkAnalyzeRequest {
  person: string;
  text: string;
  relationship?: string | null;
  save?: boolean;
}

export interface JournalAnalyzeRequest {
  person: string;
  text: string;
  save?: boolean;
}

export interface OrchestratorMessage {
  role: "user" | "system" | "assistant";
  content: string;
}

export interface OrchestratorChatRequest {
  person: string;
  messages: OrchestratorMessage[];
}

export interface SetRoleRequest {
  role: string;
}

export interface AdminSetRolesRequest {
  userId: string;
  email?: string | null;
  roles: string[];
}

export interface ChemicalCreateRequest {
  key: string;
  label: string;
  layer: string;
}

export interface InteractionCreateRequest {
  sourceChemicalId: number;
  targetChemicalId: number;
  modFactor: number;
  mechanism?: string | null;
  notes?: string | null;
}

export interface InteractionUpdateRequest {
  modFactor: number;
  mechanism?: string | null;
  notes?: string | null;
}

export interface DimensionCreateRequest {
  name: string;
  section: string;
  category: string;
  description: string;
  workRelevance: number;
  privateRelevance: number;
  sortOrder: number;
}

export interface AffinityRequest {
  chemicalId: number;
  weight: number;
}

export interface BackfillRequest {
  person?: string | null;
}

export interface CheckInRequest {
  text: string;
}

export interface CreatePersonRequest {
  name: string;
}

export interface SharePersonRequest {
  email: string;
}

export interface CreateQuestionnaireRequest {
  personName: string;
}

export interface SubmitQuestionnaireRequest {
  selectedItemIds: number[];
}

export interface SubmitSingleAnswerRequest {
  itemId: number;
}

// ═══════════════════════════════════════════════
// RESPONSE MODELS
// ═══════════════════════════════════════════════

// ── Auth ──

export interface UserInfo {
  userId: string;
  email: string;
  roles: string[];
  hasSelectedRole: boolean;
}

export interface UserWithRoles {
  userId: string;
  email: string;
  roles: string[];
  updatedAt: string;
}

// ── Persons ──

export interface PersonList {
  persons: string[];
}

export interface PersonCreated {
  personId: string;
  personalityId: string;
}

export interface CommunicationStyleResponse {
  person: string;
  communicationStyle: string;
}

export interface ChemicalCountDto {
  chemical: string;
  count: number;
}

export interface ProfileResponse {
  person: string;
  communicationStyle: string;
  chemicalCounts: ChemicalCountDto[];
  profiles: AnalysisDecision[];
}

export interface ChemicalsResponse {
  person: string;
  chemicals: ChemicalCountDto[];
}

export interface ProfileTimelineEntry {
  chemical: string;
  intensityFactor: number;
  createdAt: string;
}

export interface ProfileTimeline {
  person: string;
  entries: ProfileTimelineEntry[];
}

export interface ShareInfo {
  email: string;
  sharedAt: string;
}

export interface SharesResponse {
  shares: ShareInfo[];
}

// ── Analysis ──

export interface AnalysisDecision {
  chemical: string;
  reasoning: string;
}

export interface ChatRespondResult {
  person: string;
  sourceType: "chat";
  decisions: AnalysisDecision[];
  synthesis: string;
  layerResponses: Record<string, string>;
  suggestedResponse: string;
}

export interface AnalysisResult {
  person: string;
  sourceType: "work" | "journal";
  decisionsCount: number;
  decisions: AnalysisDecision[];
  synthesis: string;
}

// ── Dimensions ──

export interface DimensionEvidence {
  chemical: string;
  layer: string;
  reasoning: string;
  level: number;
  recency: number;
}

export interface TemporalTrajectory {
  slope: number;
  direction: string;
  r2: number;
  dataPoints: number;
  earliestLevel: number;
  latestLevel: number;
  semanticDriftDetected?: boolean;
  driftMagnitude?: number;
}

export interface ChemicalEdge {
  chemicalA: string;
  chemicalB: string;
  correlation: number;
  relationship: string;
  knownModFactor?: number | null;
  knownMechanism?: string | null;
}

export interface CircuitCoherence {
  coherenceScore: number;
  edges: ChemicalEdge[];
  pattern: string;
}

export interface DimensionScore {
  name: string;
  section: string;
  category: string;
  score: number;
  confidence: number;
  consistency: number;
  evidenceCount: number;
  evidence: DimensionEvidence[];
  trajectory?: TemporalTrajectory | null;
  circuit?: CircuitCoherence | null;
}

export interface DimensionsResponse {
  person: string;
  mode: string;
  behavioral: DimensionScore[];
  personal: DimensionScore[];
}

export interface ShadowMatrixCell {
  dimension: string;
  section: string;
  chemical: string;
  layer: string;
  shadowLevel: number;
  confidence: number;
  entryCount: number;
}

export interface ShadowMatrixResponse {
  person: string;
  mode: string;
  cells: ShadowMatrixCell[];
  dimensions: string[];
  chemicals: string[];
}

// ── Insights ──

export interface ChemicalLevelDto {
  chemical: string;
  layer: string;
  level: number;
  observationCount: number;
  variance: number;
}

export interface ChemicalProfileDto {
  person: string;
  maturity: number;
  totalObservations: number;
  uniqueChemicals: number;
  levels: ChemicalLevelDto[];
  topFive: ChemicalLevelDto[];
}

export interface ChemicalForecastDto {
  chemical: string;
  trend: string;
  currentLevel: number;
  projectedLevel: number;
  velocity: number;
  approachingOptimal: boolean;
  driftingFromOptimal: boolean;
  riskNote?: string | null;
}

export interface CascadeAlertDto {
  triggerChemical: string;
  affectedChemicals: string[];
  mechanism: string;
  severity: string;
}

export interface PersonalForecastDto {
  chemicals: ChemicalForecastDto[];
  activeCascades: CascadeAlertDto[];
  stableFoundation: string[];
  inFlux: string[];
  overallTrajectory: string;
  narrative: string;
}

export interface PrescriptionDto {
  modality: string;
  rationale: string;
  targetChemicals: string[];
  priority: number;
}

export interface HealthIndicatorsDto {
  burnoutRisk: boolean;
  burnoutRatio?: number | null;
  burnoutNote?: string | null;
  growthWindowOpen: boolean;
  growthNote?: string | null;
  overtrainingIndicator?: string | null;
  overtrainingRecommendation?: string | null;
}

export interface TrajectoryPointDto {
  date: string;
  level: number;
}

export interface ChemicalTrajectoryDto {
  chemical: string;
  layer: string;
  points: TrajectoryPointDto[];
}

export interface TrajectoryResultDto {
  person: string;
  periodDays: number;
  chemicals: ChemicalTrajectoryDto[];
}

export interface CheckInResponse {
  analysisTriggered: boolean;
  wordCount: number;
  status?: string | null;
}

export interface DashboardResultDto {
  profile: ChemicalProfileDto;
  forecast: PersonalForecastDto;
  prescriptions: PrescriptionDto[];
  health: HealthIndicatorsDto;
}

// ── Key Chemicals ──

export interface KeyChemicalDto {
  chemical: string;
  label: string;
  layer: string;
  layerColor: string;
  level: number;
  levelLabel: string;
  optimalCenter: number;
  optimalLow: number;
  optimalHigh: number;
  significance: string;
  significanceIcon: string;
  importance: number;
  observationCount: number;
}

export interface KeyChemicalsResultDto {
  person: string;
  chemicals: KeyChemicalDto[];
  narrative: string;
}

// ── Strengths & Challenges ──

export interface StrengthChallengeItemDto {
  type: string;
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
  explanation: string;
  practicalAdvice: string;
  brainExercise: string;
  relatedChemicals: string[];
  relatedLabels: string[];
}

export interface StrengthsChallengesResultDto {
  person: string;
  strengths: StrengthChallengeItemDto[];
  challenges: StrengthChallengeItemDto[];
  summary: string;
  generatedAt: string;
}

// ── Cross-Profile ──

export interface CrossProfileItemDto {
  strengthChemical: string;
  strengthLabel: string;
  challengeChemical: string;
  challengeLabel: string;
  similarity: number;
  affects: string;
  interaction: string;
  suggestion: string;
  mechanism: string;
}

export interface CrossProfileResultDto {
  person: string;
  interactions: CrossProfileItemDto[];
  narrative: string;
  generatedAt: string;
}

// ── Big Five ──

export interface BigFiveTraitScoreDto {
  trait: string;
  label: string;
  score: number;
  highSim: number;
  lowSim: number;
}

export interface BigFiveResultDto {
  person: string;
  traits: BigFiveTraitScoreDto[];
  note: string;
}

// ── MBTI ──

export interface MbtiTypeScoreDto {
  typeCode: string;
  typeLabel: string;
  similarity: number;
}

export interface MbtiResultDto {
  person: string;
  typeCode: string;
  typeLabel: string;
  rankedTypes: MbtiTypeScoreDto[];
  note: string;
}

// ── Personality Narrative ──

export interface TraitDriverDto {
  trait: string;
  label: string;
  score: number;
  narrative: string;
  pattern: string;
  keyChemicals: string[];
}

export interface MbtiInsightDto {
  cognitiveStack: string;
  strengthsNarrative: string;
  blindSpots: string;
  growthPath: string;
  dominantChemicals: string[];
}

export interface PersonalityNarrativeDto {
  person: string;
  mbtiSummary: string;
  bigFiveSummary: string;
  typeChemistry: string;
  overallPattern: string;
  mbtiInsight?: MbtiInsightDto | null;
  traitDrivers: TraitDriverDto[];
  generatedAt: string;
}

// ── Chemical Admin ──

export interface ChemicalDto {
  id: number;
  key: string;
  label: string;
  layer: string;
}

export interface ChemicalInteractionDto {
  id: number;
  sourceKey: string;
  sourceLabel: string;
  sourceLayer: string;
  targetKey: string;
  targetLabel: string;
  targetLayer: string;
  modFactor: number;
  mechanism?: string | null;
  notes?: string | null;
}

// ── Dimension Admin ──

export interface DimensionAffinity {
  chemicalKey: string;
  weight: number;
}

export interface DimensionDto {
  id: number;
  name: string;
  section: string;
  category: string;
  description: string;
  workRelevance: number;
  privateRelevance: number;
  sortOrder: number;
  affinities: DimensionAffinity[];
}

// ── Questionnaire ──

export interface QuestionnaireOption {
  id: number;
  label: string;
  text: string;
}

export interface QuestionnaireQuestion {
  sortOrder: number;
  scenario: string;
  isInverted: boolean;
  options: QuestionnaireOption[];
}

// ── Embedding Admin ──

export interface EmbeddingBackfillResponse {
  analyzed_data_embeddings: number;
  profile_embeddings: number;
  total: number;
  message: string;
}

export interface EmbeddingReembedResponse {
  mbti_deleted: number;
  bigfive_deleted: number;
  total: number;
  message: string;
}

// ── Relationships ──

export interface RelationshipType {
  name: string;
  description: string;
}

export interface RelationshipsResponse {
  relationshipTypes: RelationshipType[];
}
```

---

## DESIGN SYSTEM

### Color Palette (dark neuroscience theme)
```css
/* Background layers */
--bg-primary: #0a0e17;        /* Deep navy-black */
--bg-secondary: #111827;       /* Slightly lighter */
--bg-card: #1a1f2e;            /* Card surfaces */
--bg-hover: #242b3d;           /* Hover state */

/* Text */
--text-primary: #e2e8f0;       /* Light gray */
--text-secondary: #94a3b8;     /* Muted gray */
--text-muted: #64748b;         /* Very muted */

/* Chemical layer colors (CRITICAL — used consistently everywhere) */
--layer-neurotransmitter: #3b82f6;   /* Blue */
--layer-hormone: #10b981;             /* Emerald green */
--layer-peptide: #8b5cf6;             /* Purple */

/* Accent colors */
--accent-primary: #6366f1;     /* Indigo */
--accent-success: #22c55e;     /* Green */
--accent-warning: #f59e0b;     /* Amber */
--accent-danger: #ef4444;      /* Red */
--accent-info: #06b6d4;        /* Cyan */

/* Chart-specific */
--chart-grid: #1e293b;
--chart-text: #94a3b8;
```

### Typography
- Font: `Inter` (headings) + `JetBrains Mono` (data values)
- Base size: 14px
- All data values use monospace for alignment

### Component Patterns
- Cards: `bg-[#1a1f2e] rounded-xl border border-white/5 p-6`
- Buttons: `bg-indigo-600 hover:bg-indigo-500 rounded-lg px-4 py-2 text-sm font-medium`
- Inputs: `bg-[#111827] border border-white/10 rounded-lg px-3 py-2 text-white`
- Tables: Striped rows with `even:bg-white/[0.02]`

---

## HIGHCHARTS GLOBAL CONFIG

Apply this theme to ALL charts:

```typescript
// src/charts/highchartsTheme.ts
import Highcharts from "highcharts";

export const LAYER_COLORS = {
  neurotransmitter: "#3b82f6",
  hormone: "#10b981",
  peptide: "#8b5cf6",
} as const;

export function applyDarkTheme() {
  Highcharts.setOptions({
    chart: {
      backgroundColor: "transparent",
      style: { fontFamily: "Inter, sans-serif" },
    },
    title: { style: { color: "#e2e8f0", fontSize: "16px", fontWeight: "600" } },
    subtitle: { style: { color: "#94a3b8" } },
    xAxis: {
      gridLineColor: "#1e293b",
      labels: { style: { color: "#94a3b8" } },
      lineColor: "#1e293b",
      tickColor: "#1e293b",
    },
    yAxis: {
      gridLineColor: "#1e293b",
      labels: { style: { color: "#94a3b8" } },
    },
    legend: {
      itemStyle: { color: "#94a3b8" },
      itemHoverStyle: { color: "#e2e8f0" },
    },
    tooltip: {
      backgroundColor: "#1a1f2e",
      borderColor: "#334155",
      style: { color: "#e2e8f0" },
    },
    plotOptions: {
      series: { animation: { duration: 800 } },
    },
    colors: ["#6366f1", "#3b82f6", "#10b981", "#8b5cf6", "#f59e0b", "#ef4444", "#06b6d4", "#ec4899"],
    credits: { enabled: false },
  });
}
```

---

## HIGHCHARTS CHART SPECIFICATIONS

Generate each chart as a reusable React component in `src/charts/`. Each component receives typed props and renders `<HighchartsReact>`. Import the required Highcharts modules.

### 1. DimensionRadar.tsx — Polar/Spider chart for 24 personality dimensions

```
Required modules: highcharts/highcharts-more
Chart type: polar line
Data source: GET /api/persons/{name}/dimensions?mode=work|private
Props: { dimensions: DimensionScore[], title?: string }

Categories: dimension names around the circle (24 points)
Series[0]: "Score" — dataKey=score, 15-95 range
Series[1]: "Confidence" — dataKey=confidence × 95, semi-transparent fill

Config:
- chart.polar = true
- xAxis.categories = dimension names
- xAxis.tickmarkPlacement = "on"
- yAxis.min = 0, yAxis.max = 100
- plotOptions.series.pointPlacement = "on"
- Two series: score (solid line + fill) and confidence (dashed line, low opacity)
- Tooltip shows: name, score, confidence %, consistency %, evidenceCount
```

### 2. ChemicalLevelsBar.tsx — Grouped column chart by biochemical layer

```
Required modules: none (basic)
Chart type: column
Data source: GET /api/insights/{person}/dashboard → profile.levels
Props: { levels: ChemicalLevelDto[] }

Group by layer: neurotransmitter (blue), hormone (green), peptide (purple)
Each bar = one chemical, color = layer color
X-axis: chemical names
Y-axis: level value

Config:
- Group into 3 series by layer, each with its LAYER_COLOR
- plotOptions.column.grouping = true
- Tooltip: chemical name, level, observation count, variance
- Sort chemicals within each layer by level descending
```

### 3. ProfileTimeline.tsx — Spline chart showing chemical activity over time

```
Required modules: none (basic)
Chart type: spline
Data source: GET /api/persons/{name}/profile/timeline
Props: { timeline: ProfileTimeline }

One series per chemical (only show chemicals with 3+ data points)
X-axis: datetime (createdAt)
Y-axis: intensityFactor

Config:
- chart.type = "spline"
- xAxis.type = "datetime"
- Each series colored by chemical's layer color
- plotOptions.spline.marker.enabled = true, radius = 3
- legend.enabled = true (scrollable if many chemicals)
- Tooltip: date, chemical, intensity
```

### 4. BigFiveRadar.tsx — Polar chart for 5 OCEAN personality traits

```
Required modules: highcharts/highcharts-more
Chart type: polar line
Data source: GET /api/bigfive/{person}
Props: { result: BigFiveResultDto }

5 categories: Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism
Score range: 0 to 1 (display as 0-100%)

Config:
- chart.polar = true
- 5 categories on xAxis
- yAxis.min = 0, yAxis.max = 1
- Single series with area fill (gradient from accent color)
- Tooltip: trait name, score as %, highSim, lowSim
- Large area fill with low opacity
```

### 5. MbtiRankings.tsx — Horizontal bar chart for 16 MBTI types

```
Required modules: none (basic)
Chart type: bar (horizontal)
Data source: GET /api/mbti/{person}
Props: { result: MbtiResultDto }

16 bars, one per type, sorted by similarity descending
Highlight the best match (typeCode) with accent color, rest in muted

Config:
- chart.type = "bar"
- categories = type codes (INTJ, ENFP, etc.)
- Single series: similarity values (0-1)
- Best match bar colored #6366f1, rest #334155
- Tooltip: typeCode, typeLabel, similarity as %
- plotOptions.bar.borderRadius = 4
```

### 6. ForecastArea.tsx — Area spline showing current→projected chemical levels

```
Required modules: none (basic)
Chart type: areaspline
Data source: GET /api/insights/{person}/forecast
Props: { forecast: PersonalForecastDto }

Two data points per chemical: currentLevel → projectedLevel
Color by trend: rising=#22c55e, falling=#ef4444, stable=#6366f1

Config:
- chart.type = "areaspline"
- One series per chemical
- 2 x-axis points: "Current" and "Projected (30d)"
- Color each series by its trend direction
- fillOpacity = 0.1
- Tooltip: chemical, current, projected, velocity, riskNote
```

### 7. ShadowHeatmap.tsx — Heatmap of chemical×dimension shadow levels

```
Required modules: highcharts/modules/heatmap
Chart type: heatmap
Data source: GET /api/persons/{name}/shadow-matrix
Props: { matrix: ShadowMatrixResponse }

X-axis: chemicals (matrix.chemicals)
Y-axis: dimensions (matrix.dimensions)
Color: shadowLevel (1-100 scale)

Config:
- Requires: import Heatmap from "highcharts/modules/heatmap"
- colorAxis.min = 0, colorAxis.max = 100
- colorAxis.stops = [[0, "#1e293b"], [0.5, "#6366f1"], [1, "#f59e0b"]]
- data = cells.map(c => [chemIndex, dimIndex, c.shadowLevel])
- Tooltip: dimension, chemical, shadowLevel, confidence, entryCount
- Scrollable if many dimensions
```

### 8. BurnoutGauge.tsx — Solid gauge showing burnout risk ratio

```
Required modules: highcharts/highcharts-more, highcharts/modules/solid-gauge
Chart type: solidgauge
Data source: GET /api/insights/{person}/health
Props: { health: HealthIndicatorsDto }

Single gauge showing burnoutRatio (0-100 scale, null=50 default)
Color zones: 0-30 green, 30-60 amber, 60-100 red

Config:
- Requires: import HighchartsMore + SolidGauge modules
- yAxis.min = 0, yAxis.max = 100
- yAxis.stops = [[0.3, "#22c55e"], [0.6, "#f59e0b"], [1, "#ef4444"]]
- pane.startAngle = -90, endAngle = 90 (semicircle)
- Display burnoutRatio value centered
- Subtitle: burnoutNote text
```

### 9. TrajectorySparklines.tsx — Grid of mini spline charts per chemical

```
Required modules: none (basic)
Chart type: spline (mini, no axes labels)
Data source: GET /api/insights/{person}/trajectory
Props: { trajectory: TrajectoryResultDto }

Grid layout: 3-4 columns of small sparkline charts
Each chart = one chemical's points over time

Config per sparkline:
- chart.type = "spline", chart.height = 80, chart.width = 200
- chart.margin = [5, 5, 5, 5]
- xAxis/yAxis.visible = false
- title.text = chemical name (small)
- series color = layer color
- No legend, no tooltip (or minimal tooltip)
- plotOptions.spline.lineWidth = 2
```

### 10. StrengthsChallengesBar.tsx — Diverging bar chart from optimal center

```
Required modules: none (basic)
Chart type: bar (horizontal, diverging)
Data source: GET /api/insights/{person}/strengths-challenges
Props: { result: StrengthsChallengesResultDto }

Strengths: positive bars (green), Challenges: negative bars (red)
Each bar = deviation from optimalCenter

Config:
- Merge strengths (positive deviation) and challenges (negative deviation) into one chart
- Two series: "Strengths" (#22c55e) and "Challenges" (#ef4444)
- plotOptions.bar.stacking = undefined (side by side is fine)
- Tooltip: title, chemical, level, optimalCenter, deviation, explanation
```

### 11. KeyChemicalsDonut.tsx — Donut/pie chart for top chemicals by importance

```
Required modules: none (basic)
Chart type: pie (donut)
Data source: GET /api/insights/{person}/key-chemicals
Props: { result: KeyChemicalsResultDto }

Inner ring hollow (donut), slices = chemicals by importance
Color each slice by layerColor from the DTO

Config:
- chart.type = "pie"
- plotOptions.pie.innerSize = "60%"
- data = chemicals.map(c => ({ name: c.label, y: c.importance, color: c.layerColor }))
- Tooltip: label, level, levelLabel, significance, observationCount
- Center text: total observation count or narrative snippet
```

### 12. CrossProfileNetwork.tsx — Network graph of strength↔challenge interactions

```
Required modules: highcharts/modules/networkgraph
Chart type: networkgraph
Data source: GET /api/insights/{person}/cross-profile
Props: { result: CrossProfileResultDto }

Nodes = unique chemicals (from both strength and challenge sides)
Links = interactions with similarity as link weight

Config:
- Requires: import Networkgraph from "highcharts/modules/networkgraph"
- series.type = "networkgraph"
- data = interactions.map(i => [i.strengthChemical, i.challengeChemical])
- Node colors: strength nodes = green, challenge nodes = red
- Link thickness = similarity × 5
- Tooltip on node: chemical name
- Tooltip on link: affects, mechanism, suggestion
```

### 13. CascadeFlow.tsx — Sankey diagram for chemical cascades

```
Required modules: highcharts/modules/sankey
Chart type: sankey
Data source: GET /api/insights/{person}/forecast → activeCascades
Props: { cascades: CascadeAlertDto[] }

Left nodes: triggerChemical
Right nodes: affectedChemicals (expand array)
Links: trigger → each affected chemical

Config:
- Requires: import Sankey from "highcharts/modules/sankey"
- series.type = "sankey"
- data = cascades.flatMap(c => c.affectedChemicals.map(a => [c.triggerChemical, a, 1]))
- Node color by severity: low=#22c55e, medium=#f59e0b, high=#ef4444
- Tooltip: mechanism, severity
```

---

## PAGE SPECIFICATIONS

### Personal User Pages

#### 1. JournalEntryPage.tsx
- Large textarea for journal text (min 200px height)
- Submit button → `POST /api/analyze/journal` with `{ person: activePerson, text, save: true }`
- While loading: show animated pulse skeleton
- On response: show DecisionCards (grid of chemical decisions with reasoning) + SynthesisPanel
- Each DecisionCard: colored left border by layer, chemical name bold, reasoning text below

#### 2. JournalHistoryPage.tsx
- Fetch `GET /api/persons/{name}/profile/timeline`
- Render `<ProfileTimeline>` chart at top
- Below: scrollable list of entries grouped by date
- Each entry shows: chemical pill (colored by layer), intensityFactor, timestamp

#### 3. MyProfilePage.tsx
- Fetch `GET /api/persons/{name}/dimensions?mode=private` and `?mode=work`
- Tab switcher: "Private" | "Work"
- Render `<DimensionRadar>` for selected mode
- Below radar: expandable cards per dimension showing evidence, trajectory, circuit coherence
- Sidebar: communication style text from `GET /api/persons/{name}/style`

#### 4. MyChemistryPage.tsx
- Fetch dashboard + key-chemicals + strengths-challenges in parallel
- Top row: `<KeyChemicalsDonut>` + `<ChemicalLevelsBar>`
- Middle: `<StrengthsChallengesBar>`
- Bottom: Strength/Challenge detail cards with practicalAdvice and brainExercise

#### 5. InsightsDashboardPage.tsx
- Fetch `GET /api/insights/{person}/dashboard`
- Layout grid:
  - Top-left: `<BurnoutGauge>` + health indicators text
  - Top-right: `<ForecastArea>` with forecast narrative
  - Middle: `<TrajectorySparklines>` grid
  - Bottom-left: Prescriptions list (sorted by priority)
  - Bottom-right: `<CascadeFlow>` if activeCascades.length > 0
- Each prescription card: modality icon, rationale text, target chemical pills

#### 6. PersonalityPage.tsx
- Fetch MBTI + Big Five + personality narrative in parallel
- Left column: `<BigFiveRadar>` + trait driver cards
- Right column: `<MbtiRankings>` + MBTI insight panel (cognitive stack, strengths, blind spots, growth path)
- Bottom: full personality narrative text

#### 7. CheckInPage.tsx
- Simple centered card with textarea: "How are you feeling?"
- Submit → `POST /api/insights/{person}/checkin`
- Show response: analysisTriggered badge, wordCount, status
- If analysis triggered: "Your check-in has been analyzed. Visit your dashboard for updated insights."

### Professional User Pages

#### 1. AnalyzeDocumentPage.tsx
- Person selector dropdown (or create new person)
- Large textarea: "Paste CV or document text here"
- Optional: relationship type dropdown from `GET /api/relationships/`
- Submit → `POST /api/analyze/work`
- Results: DecisionCards + SynthesisPanel
- Action button: "View Full Profile →" navigates to CandidateProfilePage

#### 2. CandidateListPage.tsx
- Fetch `GET /api/persons/`
- Searchable/filterable table of persons
- Each row: person name, quick stats (if available)
- Click → navigate to CandidateProfilePage
- "Add Candidate" button → create person dialog

#### 3. CandidateProfilePage.tsx
- Route param: person name
- Fetch all data in parallel: profile, dimensions (work mode), MBTI, Big Five, key-chemicals, strengths-challenges, dashboard
- Top: person name + MBTI type badge + communication style
- Tab sections:
  - "Overview": `<DimensionRadar>` + `<BigFiveRadar>` side by side
  - "Chemistry": `<ChemicalLevelsBar>` + `<KeyChemicalsDonut>`
  - "Strengths": `<StrengthsChallengesBar>` + detail cards
  - "Health": `<BurnoutGauge>` + health notes
  - "Timeline": `<ProfileTimeline>` + `<TrajectorySparklines>`
  - "Shadow Matrix": `<ShadowHeatmap>`

#### 4. ChatAnalysisPage.tsx
- Person selector
- Relationship type selector + optional projected relationship
- Large textarea: "Paste conversation"
- Submit → `POST /api/analyze/chat`
- Results: DecisionCards + layer responses (collapsible per layer) + suggested response highlight box + synthesis

### Admin Pages

#### 1. UserManagementPage.tsx
- Fetch `GET /api/auth/admin/users`
- Table: userId, email, roles (as pills), updatedAt
- Click user → modal to edit roles via `POST /api/auth/admin/set-roles`
- Available roles: `private`, `work`, `both`, `worker`, `admin`

#### 2. ChemicalManagementPage.tsx
- Fetch `GET /api/chemicals/`
- Table: id, key, label, layer (colored badge)
- Add/Edit/Delete with modals
- Layer dropdown: neurotransmitter, hormone, peptide

#### 3. InteractionManagementPage.tsx
- Fetch `GET /api/chemical-interactions/`
- Table: source→target, modFactor, mechanism
- Add: select source + target chemicals, set modFactor, mechanism, notes
- Visual: `<CrossProfileNetwork>` showing all known interactions

#### 4. DimensionManagementPage.tsx
- Fetch `GET /api/dimensions/`
- Table: name, section, category, relevance weights, affinity count
- Expandable row: shows affinities (chemical + weight)
- Add/edit dimension modal
- Add/remove affinities inline

#### 5. EmbeddingAdminPage.tsx
- Two action cards:
  - "Backfill Embeddings" → `POST /api/embeddings/backfill` with optional person filter
  - "Reembed Prototypes" → `POST /api/embeddings/reembed-prototypes`
- Show result stats after each operation
- Warning text: "Reembedding clears cached prototypes. MBTI and Big Five will regenerate on next classification."

---

## SIDEBAR NAVIGATION

```
── Personal User ──
📝 Journal Entry
📖 Journal History
👤 My Profile
🧪 My Chemistry
📊 Insights Dashboard
🧠 Personality
💬 Quick Check-in

── Professional User ──
📄 Analyze Document
👥 Candidates
💬 Chat Analysis

── Admin ──
👥 User Management
🧪 Chemicals
🔗 Interactions
📐 Dimensions
⚡ Embeddings
```

Users with role `both` see Personal + Professional sections.
Users with role `admin` see all sections.

---

## IMPORTANT IMPLEMENTATION NOTES

1. **Person context**: The app needs a "selected person" concept. Personal users = themselves (auto-created on first login). Professional users select from the persons list. Store in Zustand.

2. **API base URL**: Default to `http://localhost:13370`. Make configurable via `VITE_API_URL` env var.

3. **Error handling**: All API calls should show toast notifications on error. Use a simple toast system (not a library dependency).

4. **Loading states**: Every page should show skeleton loaders while fetching data. Never show a blank page.

5. **Responsive**: Desktop-first but must work on tablet. Sidebar collapses to hamburger menu on narrow screens.

6. **Highcharts modules**: Import and initialize required modules (heatmap, networkgraph, sankey, solid-gauge, more) in main.tsx before any chart renders.

7. **Layer color consistency**: ALWAYS use the same colors for chemical layers throughout the entire app: neurotransmitter=#3b82f6, hormone=#10b981, peptide=#8b5cf6. The backend provides `layerColor` in some DTOs — use that when available, fall back to the constants.

8. **Date formatting**: Use `Intl.DateTimeFormat` for locale-aware dates. No moment.js or date-fns needed.

9. **Analysis can be slow**: The analyze endpoints (chat, work, journal) run 27 parallel AI agents and can take 30-60 seconds. Show a progress indicator with "Running biochemical analysis..." text.

10. **Highcharts license**: Highcharts requires a license for commercial use. Add a comment in the code noting this.

---

## GENERATE ALL FILES

Generate every file listed in the project structure above. Every page, every chart component, every API module, every type, every store, the complete auth flow, the layout, the sidebar, and the routing configuration. The application should be immediately runnable with `npm install && npm run dev`.
