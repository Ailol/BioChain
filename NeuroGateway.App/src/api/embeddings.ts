import api from './client';
import type { BackfillRequest, EmbeddingBackfillResponse } from '@/types';

export const embeddingsApi = {
  backfill: (data?: BackfillRequest) => api.post<EmbeddingBackfillResponse>('/api/embeddings/backfill', data ?? {}).then(r => r.data),
};
