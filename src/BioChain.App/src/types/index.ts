// ── Auth ──────────────────────────────────────────────────────
export interface UserInfo {
  userId: string;
  email: string;
  roles: string[];
  hasSelectedRole: boolean;
}

// ── Chat ─────────────────────────────────────────────────────
export interface ChatHistoryItem {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatRequest {
  subjectId: string;
  message: string;
  history?: ChatHistoryItem[];
}

export interface ChatResponse {
  response: string;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  analyzing?: boolean;
}

// ── Questionnaire ────────────────────────────────────────────
export interface QuestionOption {
  id: number;
  label: string;
  text: string;
}

export interface Question {
  sortOrder: number;
  scenario: string;
  options: QuestionOption[];
}

export interface QuestionsResponse {
  questions: Question[];
}

export interface QuestionnaireSubmitRequest {
  subjectId: string;
  answers: { sortOrder: number; selectedItemId: number }[];
}

export interface QuestionnaireSubmitResponse {
  questionnaireId: string;
  stimuliIds: number[];
  protocolsStored: number;
  linesTotal: number;
}

// ── Subject ─────────────────────────────────────────────────
export interface Subject {
  id: string;
  name: string;
  kind: string;
  createdAt: string;
}

export interface CreateSubjectRequest {
  name: string;
}

// ── Analyze ──────────────────────────────────────────────────
export interface AnalyzeRequest {
  subjectId: string;
  text: string;
  kind: string;
}

export interface AnalyzeResponse {
  stimuliId: number;
  protocolsStored: number;
  linesTotal: number;
}
