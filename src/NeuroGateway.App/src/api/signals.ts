import api from './client';
import type { SignalDto } from '@/types';

export const signalsApi = {
  list: () => api.get<SignalDto[]>('/api/signals/').then(r => r.data),
  byLayer: (layer: string) => api.get<SignalDto[]>(`/api/signals/by-layer/${encodeURIComponent(layer)}`).then(r => r.data),
  byKey: (key: string) => api.get<SignalDto>(`/api/signals/${encodeURIComponent(key)}`).then(r => r.data),
};
