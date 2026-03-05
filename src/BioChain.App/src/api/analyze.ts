import api from './client';
import type { AnalyzeRequest, AnalyzeResponse } from '@/types';

export const analyzeApi = {
  analyze: async (req: AnalyzeRequest): Promise<AnalyzeResponse> => {
    const { data } = await api.post<AnalyzeResponse>('/api/analyze', req);
    return data;
  },
};
