import api from './client';
import type { SignalInteractionDto } from '@/types';

export const signalInteractionsApi = {
  list: () => api.get<SignalInteractionDto[]>('/api/signal-interactions/').then(r => r.data),
  bySignal: (signalKey: string) => api.get<SignalInteractionDto[]>(`/api/signal-interactions/${encodeURIComponent(signalKey)}`).then(r => r.data),
};
