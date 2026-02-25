import api from './client';
import type {
  PersonList, PersonCreated, CreatePersonRequest, ProfileResponse,
  CommunicationStyleResponse, SignalsResponse, ProfileTimeline,
  SharePersonRequest, SharesResponse, DimensionsResponse, ShadowMatrixResponse,
} from '@/types';

export const personsApi = {
  list: () => api.get<PersonList>('/api/persons/').then(r => r.data),
  create: (data: CreatePersonRequest) => api.post<PersonCreated>('/api/persons/', data).then(r => r.data),
  getProfile: (name: string) => api.get<ProfileResponse>(`/api/persons/${encodeURIComponent(name)}/profile`).then(r => r.data),
  getStyle: (name: string) => api.get<CommunicationStyleResponse>(`/api/persons/${encodeURIComponent(name)}/style`).then(r => r.data),
  getSignals: (name: string) => api.get<SignalsResponse>(`/api/persons/${encodeURIComponent(name)}/signals`).then(r => r.data),
  getTimeline: (name: string) => api.get<ProfileTimeline>(`/api/persons/${encodeURIComponent(name)}/profile/timeline`).then(r => r.data),
  share: (name: string, data: SharePersonRequest) => api.post(`/api/persons/${encodeURIComponent(name)}/share`, data).then(r => r.data),
  unshare: (name: string, email: string) => api.delete(`/api/persons/${encodeURIComponent(name)}/share?email=${encodeURIComponent(email)}`).then(r => r.data),
  getShares: (name: string) => api.get<SharesResponse>(`/api/persons/${encodeURIComponent(name)}/shares`).then(r => r.data),
  getDimensions: (name: string, mode: 'work' | 'private') => api.get<DimensionsResponse>(`/api/persons/${encodeURIComponent(name)}/dimensions?mode=${mode}`).then(r => r.data),
  getShadowMatrix: (name: string, mode: 'work' | 'private') => api.get<ShadowMatrixResponse>(`/api/persons/${encodeURIComponent(name)}/shadow-matrix?mode=${mode}`).then(r => r.data),
};
