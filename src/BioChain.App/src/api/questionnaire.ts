import api from './client';
import type {
  QuestionsResponse,
  QuestionnaireSubmitRequest,
  QuestionnaireSubmitResponse,
} from '@/types';

export const questionnaireApi = {
  getQuestions: async (): Promise<QuestionsResponse> => {
    const { data } = await api.get<QuestionsResponse>('/api/questionnaire/questions');
    return data;
  },

  submit: async (req: QuestionnaireSubmitRequest): Promise<QuestionnaireSubmitResponse> => {
    const { data } = await api.post<QuestionnaireSubmitResponse>(
      '/api/questionnaire/submit',
      req,
    );
    return data;
  },
};
