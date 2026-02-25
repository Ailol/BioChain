import { create } from 'zustand';
import type { UserInfo } from '@/types';
import { expandEffectiveRoles } from '@/utils/roles';

interface AuthState {
  userId: string | null;
  email: string | null;
  roles: string[];
  effectiveRoles: string[];
  hasSelectedRole: boolean;
  isAuthenticated: boolean;
  isInitialized: boolean;

  setUser: (info: UserInfo) => void;
  setInitialized: (value: boolean) => void;
  clear: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  userId: null,
  email: null,
  roles: [],
  effectiveRoles: [],
  hasSelectedRole: false,
  isAuthenticated: false,
  isInitialized: false,

  setUser: (info) =>
    set({
      userId: info.userId,
      email: info.email,
      roles: info.roles,
      effectiveRoles: expandEffectiveRoles(info.roles),
      hasSelectedRole: info.hasSelectedRole,
      isAuthenticated: true,
    }),

  setInitialized: (value) => set({ isInitialized: value }),

  clear: () =>
    set({
      userId: null,
      email: null,
      roles: [],
      effectiveRoles: [],
      hasSelectedRole: false,
      isAuthenticated: false,
      isInitialized: false,
    }),
}));
