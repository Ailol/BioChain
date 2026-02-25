import api from './client';
import type {
  ChatAnalyzeRequest, WorkAnalyzeRequest, JournalAnalyzeRequest,
  OrchestratorChatRequest, ChatRespondResult, AnalysisResult,
} from '@/types';

export const analyzeApi = {
  chat: (data: ChatAnalyzeRequest) => api.post<ChatRespondResult>('/api/analyze/chat', data).then(r => r.data),
  work: (data: WorkAnalyzeRequest) => api.post<AnalysisResult>('/api/analyze/work', data).then(r => r.data),
  journal: (data: JournalAnalyzeRequest) => api.post<AnalysisResult>('/api/analyze/journal', data).then(r => r.data),
  orchestrator: (data: OrchestratorChatRequest) => api.post<{ response: string }>('/api/analyze/orchestrator', data).then(r => r.data),
};
