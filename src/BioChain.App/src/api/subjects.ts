import api from './client';
import type { Subject, CreateSubjectRequest } from '@/types';

export const subjectsApi = {
  list: async (): Promise<Subject[]> => {
    const { data } = await api.get<Subject[]>('/api/subjects');
    return data;
  },

  create: async (req: CreateSubjectRequest): Promise<Subject> => {
    const { data } = await api.post<Subject>('/api/subjects', req);
    return data;
  },
};
