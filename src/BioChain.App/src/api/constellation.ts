import api from './client';
import type { ConstellationGraphResponse, ConstellationAnalysisResponse } from '@/types/constellation';

export const constellationApi = {
  getGraph: async (subjectId: string, signal?: AbortSignal): Promise<ConstellationGraphResponse> => {
    const { data } = await api.get<ConstellationGraphResponse>(
      `/api/constellation/graph/${subjectId}`,
      { signal },
    );
    return data;
  },

  analyze: async (subjectId: string, signal?: AbortSignal): Promise<ConstellationAnalysisResponse> => {
    const { data } = await api.post<ConstellationAnalysisResponse>(
      `/api/constellation/analyze/${subjectId}`,
      undefined,
      { signal, timeout: 600000 },
    );
    return data;
  },
};
