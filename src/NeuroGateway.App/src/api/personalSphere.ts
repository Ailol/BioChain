import api from './client';
import type { PersonalSphereResponse } from '@/types';

export const personalSphereApi = {
  get: (person: string): Promise<PersonalSphereResponse> =>
    api.get(`/api/personal-sphere/${encodeURIComponent(person)}`).then((r) => r.data),
};
