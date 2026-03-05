import api from './client';
import type { ChatRequest, ChatResponse } from '@/types';

export const chatApi = {
  send: async (req: ChatRequest): Promise<ChatResponse> => {
    const { data } = await api.post<ChatResponse>('/api/chat', req);
    return data;
  },
};
