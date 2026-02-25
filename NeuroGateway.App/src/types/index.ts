// ============================================================================
// NeuroReact TypeScript Interfaces
// All backend "chemical" references have been refactored to "signal".
// ============================================================================

// ----------------------------------------------------------------------------
// Request Models
// ----------------------------------------------------------------------------

export interface ChatAnalyzeRequest {
  person: string;
  text: string;
  relationship?: string;
  projectedRelationship?: string;
  save?: boolean;
}

export interface WorkAnalyzeRequest {
  person: string;
  text: string;
  relationship?: string;
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
  email?: string;
  roles: string[];
}

export interface SignalCreateRequest {
  key: string;
  label: string;
  layer: string;
}

export interface InteractionCreateRequest {
  sourceSignalId: number;
  targetSignalId: number;
  modFactor: number;
  mechanism?: string;
  notes?: string;
}

export interface InteractionUpdateRequest {
  modFactor: number;
  mechanism?: string;
  notes?: string;
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
  signalId: number;
  weight: number;
}

export interface BackfillRequest {
  person?: string;
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

// ----------------------------------------------------------------------------
// Response Models
// ----------------------------------------------------------------------------

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

export interface SignalCountDto {
  signal: string;
  count: number;
}

export interface ProfileResponse {
  person: string;
  communicationStyle: string;
  signalCounts: SignalCountDto[];
  profiles: AnalysisDecision[];
}

export interface SignalsResponse {
  person: string;
  signals: SignalCountDto[];
}

export interface ProfileTimelineEntry {
  signal: string;
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

// ----------------------------------------------------------------------------
// Analysis Models
// ----------------------------------------------------------------------------

export interface AnalysisDecision {
  signal: string;
  signalId?: number;
  signals?: string[];
  formula: string;
  state?: string;
  circuits?: string;
  subjectState?: string;
  operator?: string;
  targetSignalId?: number;
  targetState?: string;
  regionId?: string;
  temporal?: string;
  confidence?: number;
  failureMode?: string;
  intensity?: number;
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

// ----------------------------------------------------------------------------
// Dimension Models
// ----------------------------------------------------------------------------

export interface DimensionEvidence {
  signal: string;
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

export interface SignalEdge {
  signalA: string;
  signalB: string;
  correlation: number;
  relationship: string;
  knownModFactor?: number;
  knownMechanism?: string;
}

export interface CircuitCoherence {
  coherenceScore: number;
  edges: SignalEdge[];
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
  trajectory?: TemporalTrajectory;
  circuit?: CircuitCoherence;
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
  signal: string;
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
  signals: string[];
}

// ----------------------------------------------------------------------------
// Insight Models
// ----------------------------------------------------------------------------

export interface SignalLevelDto {
  signal: string;
  layer: string;
  level: number;
  observationCount: number;
  variance: number;
}

export interface SignalProfileDto {
  person: string;
  maturity: string;
  totalObservations: number;
  uniqueSignals: number;
  levels: SignalLevelDto[];
  topFive: SignalLevelDto[];
}

export interface SignalForecastDto {
  signal: string;
  trend: string;
  currentLevel: number;
  projectedLevel: number;
  velocity: number;
  approachingOptimal: boolean;
  driftingFromOptimal: boolean;
  riskNote?: string;
}

export interface CascadeAlertDto {
  triggerSignal: string;
  affectedSignals: string[];
  mechanism: string;
  severity: string;
}

export interface PersonalForecastDto {
  signals: SignalForecastDto[];
  activeCascades: CascadeAlertDto[];
  stableFoundation: string[];
  inFlux: string[];
  overallTrajectory: string;
  narrative: string;
}

export interface PrescriptionDto {
  modality: string;
  rationale: string;
  targetSignals: string[];
  priority: string;
}

export interface HealthIndicatorsDto {
  burnoutRisk: string;
  burnoutRatio?: number;
  burnoutNote?: string;
  growthWindowOpen: boolean;
  growthNote?: string;
  overtrainingIndicator?: string;
  overtrainingRecommendation?: string;
}

export interface TrajectoryPointDto {
  date: string;
  level: number;
}

export interface SignalTrajectoryDto {
  signal: string;
  layer: string;
  points: TrajectoryPointDto[];
}

export interface TrajectoryResultDto {
  person: string;
  periodDays: number;
  signals: SignalTrajectoryDto[];
}

export interface CheckInResponse {
  analysisTriggered: boolean;
  wordCount: number;
  status?: string;
}

export interface DashboardResultDto {
  profile: SignalProfileDto;
  forecast: PersonalForecastDto;
  prescriptions: PrescriptionDto[];
  health: HealthIndicatorsDto;
}

// ----------------------------------------------------------------------------
// Key Signals Models
// ----------------------------------------------------------------------------

export interface KeySignalDto {
  signal: string;
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

export interface KeySignalsResultDto {
  person: string;
  signals: KeySignalDto[];
  narrative: string;
}

// ----------------------------------------------------------------------------
// Strengths & Challenges
// ----------------------------------------------------------------------------

export interface StrengthChallengeItemDto {
  type: string;
  indicator: string;
  title: string;
  signalKey: string;
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
  relatedSignals: string[];
  relatedLabels: string[];
}

export interface StrengthsChallengesResultDto {
  person: string;
  strengths: StrengthChallengeItemDto[];
  challenges: StrengthChallengeItemDto[];
  summary: string;
  generatedAt: string;
}

// ----------------------------------------------------------------------------
// Cross-Profile
// ----------------------------------------------------------------------------

export interface CrossProfileItemDto {
  strengthSignal: string;
  strengthLabel: string;
  challengeSignal: string;
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

// ----------------------------------------------------------------------------
// Personality Narrative
// ----------------------------------------------------------------------------

export interface TraitDriverDto {
  trait: string;
  label: string;
  score: number;
  narrative: string;
  pattern: string;
  keySignals: string[];
}

export interface MbtiInsightDto {
  cognitiveStack: string;
  strengthsNarrative: string;
  blindSpots: string;
  growthPath: string;
  dominantSignals: string[];
}

export interface PersonalityNarrativeDto {
  person: string;
  mbtiSummary: string;
  bigFiveSummary: string;
  typeChemistry: string;
  overallPattern: string;
  mbtiInsight?: MbtiInsightDto;
  traitDrivers: TraitDriverDto[];
  generatedAt: string;
}

// ----------------------------------------------------------------------------
// Signal Admin
// ----------------------------------------------------------------------------

export interface SignalDto {
  id: number;
  key: string;
  label: string;
  layer: string;
  code?: string;
  unit?: string;
}

export interface SignalInteractionDto {
  id: number;
  sourceKey: string;
  sourceLabel: string;
  sourceLayer: string;
  targetKey: string;
  targetLabel: string;
  targetLayer: string;
  modFactor: number;
  mechanism?: string;
  notes?: string;
}

// ----------------------------------------------------------------------------
// Dimension Admin
// ----------------------------------------------------------------------------

export interface DimensionAffinity {
  signalKey: string;
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

// ----------------------------------------------------------------------------
// Big Five
// ----------------------------------------------------------------------------

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

// ----------------------------------------------------------------------------
// MBTI
// ----------------------------------------------------------------------------

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

// ----------------------------------------------------------------------------
// Questionnaire
// ----------------------------------------------------------------------------

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

// ----------------------------------------------------------------------------
// Embedding Admin
// ----------------------------------------------------------------------------

export interface EmbeddingBackfillResponse {
  analyzed_data_embeddings: number;
  profile_embeddings: number;
  total: number;
  message: string;
}

// ----------------------------------------------------------------------------
// Relationships
// ----------------------------------------------------------------------------

export interface RelationshipType {
  name: string;
  description: string;
}

export interface RelationshipsResponse {
  relationshipTypes: RelationshipType[];
}

// ----------------------------------------------------------------------------
// BioSphere Models
// ----------------------------------------------------------------------------

export interface BioSphereSignalProfile {
  signal: string;
  label: string;
  value: number;
  state: string;
  region: string;
  trend: string;
}

export interface BioSphereRadarPoint {
  dim: string;
  value: number;
  fullMark: number;
}

export interface BioSphereTrajectoryPoint {
  phase: string;
  label: string;
  [signal: string]: number | string;
}

export interface BioSphereLoop {
  name: string;
  type: string;
  status: string;
  severity: string;
  formula: string;
  signals: string[];
}

export interface BioSphereCascadeTarget {
  name: string;
  impact: number;
}

export interface BioSphereCascade {
  source: string;
  targets: BioSphereCascadeTarget[];
}

export interface BioSphereGate {
  gate: string;
  instance: string;
  formula: string;
  status: string;
}

export interface BioSphereRegionHeatmap {
  region: string;
  [signal: string]: number | string;
}

export interface BioSphereFailureMode {
  name: string;
  size: number;
  severity: string;
  color: string;
}

export interface BioSphereLifecycleStage {
  stage: string;
  healthy: number;
  current: number;
  vulnerable: boolean;
}

export interface BioSphereResponse {
  person: string;
  lastAnalysis: string;
  signalProfile: BioSphereSignalProfile[];
  radar: BioSphereRadarPoint[];
  trajectory: BioSphereTrajectoryPoint[];
  loops: BioSphereLoop[];
  cascades: BioSphereCascade[];
  gates: BioSphereGate[];
  regionHeatmap: BioSphereRegionHeatmap[];
  failureModes: BioSphereFailureMode[];
  lifecycle: BioSphereLifecycleStage[];
}

// ----------------------------------------------------------------------------
// PersonalSphere Models
// ----------------------------------------------------------------------------

export interface PersonalSphereInsight {
  id: string;
  title: string;
  body: string;
  why: string;
  formulas: string[];
  signals: Record<string, number>;
  color: string;
  colorDim: string;
  colorGlow: string;
  domain: string;
}

export interface PersonalSpherePattern {
  title: string;
  body: string;
  formula: string;
  icon: string;
}

export interface PersonalSphereLeveragePoint {
  rank: number;
  title: string;
  description: string;
  impact: number;
  feasibility: number;
  signals: string[];
  color: string;
}

export interface PersonalSphereStrength {
  title: string;
  detail: string;
  signal: string;
  color: string;
}

export interface PersonalSphereSystemRadar {
  system: string;
  healthy: number;
  current: number;
}

export interface PersonalSphereEnergyCurve {
  hour: number;
  healthy: number;
  current: number;
}

export interface PersonalSphereResponse {
  person: string;
  coreInsights: PersonalSphereInsight[];
  deepPatterns: PersonalSpherePattern[];
  leveragePoints: PersonalSphereLeveragePoint[];
  strengths: PersonalSphereStrength[];
  systemRadar: PersonalSphereSystemRadar[];
  energyCurve: PersonalSphereEnergyCurve[];
}
