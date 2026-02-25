import api from './client';
import type { BioSphereResponse } from '@/types';

export const biosphereApi = {
  get: (person: string): Promise<BioSphereResponse> =>
    api.get(`/api/biosphere/${encodeURIComponent(person)}`).then((r) => r.data),
};
