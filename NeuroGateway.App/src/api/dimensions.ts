import api from './client';
import type { DimensionDto, DimensionCreateRequest, AffinityRequest } from '@/types';

export const dimensionsApi = {
  list: () => api.get<DimensionDto[]>('/api/dimensions/').then(r => r.data),
  create: (data: DimensionCreateRequest) => api.post('/api/dimensions/', data).then(r => r.data),
  update: (id: number, data: DimensionCreateRequest) => api.put(`/api/dimensions/${id}`, data),
  remove: (id: number) => api.delete(`/api/dimensions/${id}`),
  setAffinity: (id: number, data: AffinityRequest) => api.put(`/api/dimensions/${id}/affinities`, data),
  removeAffinity: (dimId: number, signalId: number) => api.delete(`/api/dimensions/${dimId}/affinities/${signalId}`),
};
