import api from './client';
import type { UserInfo, UserWithRoles, SetRoleRequest, AdminSetRolesRequest } from '@/types';

export const authApi = {
  getMe: () => api.get<UserInfo>('/api/auth/me').then(r => r.data),
  setRole: (data: SetRoleRequest) => api.post('/api/auth/set-role', data).then(r => r.data),
  syncRoles: () => api.post('/api/auth/sync-roles').then(r => r.data),
  resolveShares: () => api.post('/api/auth/resolve-shares').then(r => r.data),
  getAdminUsers: () => api.get<{ users: UserWithRoles[] }>('/api/auth/admin/users').then(r => r.data),
  setAdminRoles: (data: AdminSetRolesRequest) => api.post('/api/auth/admin/set-roles', data).then(r => r.data),
};
