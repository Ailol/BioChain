import api from './client';
import type { QuestionnaireQuestion, CreateQuestionnaireRequest, SubmitQuestionnaireRequest, SubmitSingleAnswerRequest } from '@/types';

export const questionnaireApi = {
  getQuestions: () => api.get<{ questions: QuestionnaireQuestion[] }>('/api/questionnaire/questions').then(r => r.data),
  create: (data: CreateQuestionnaireRequest) => api.post<{ token: string }>('/api/questionnaire/', data).then(r => r.data),
  get: (token: string) => api.get(`/api/questionnaire/${token}`).then(r => r.data),
  submit: (token: string, data: SubmitQuestionnaireRequest) => api.post(`/api/questionnaire/${token}/submit`, data).then(r => r.data),
  answer: (token: string, data: SubmitSingleAnswerRequest) => api.post(`/api/questionnaire/${token}/answer`, data).then(r => r.data),
};
