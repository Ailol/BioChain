import api from './client';
import type { UserInfo } from '@/types';

export const authApi = {
  getMe: async (): Promise<UserInfo> => {
    const { data } = await api.get('/api/auth/me');
    return data;
  },

  syncRoles: async () => {
    await api.post('/api/auth/sync-roles');
  },

  resolveShares: async () => {
    await api.post('/api/auth/resolve-shares');
  },

  setRole: async (role: string) => {
    await api.post('/api/auth/set-role', { role });
  },
};
