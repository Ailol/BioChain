import api from './client';
import type { RelationshipsResponse } from '@/types';

export const relationshipsApi = {
  list: () => api.get<RelationshipsResponse>('/api/relationships/').then(r => r.data),
};
