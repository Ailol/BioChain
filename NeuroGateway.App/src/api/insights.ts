import api from './client';
import type {
  DashboardResultDto, PersonalForecastDto, PrescriptionDto,
  HealthIndicatorsDto, TrajectoryResultDto, KeySignalsResultDto,
  StrengthsChallengesResultDto, CrossProfileResultDto, CheckInRequest, CheckInResponse,
} from '@/types';

export const insightsApi = {
  dashboard: (person: string) => api.get<DashboardResultDto>(`/api/insights/${encodeURIComponent(person)}/dashboard`).then(r => r.data),
  forecast: (person: string) => api.get<PersonalForecastDto>(`/api/insights/${encodeURIComponent(person)}/forecast`).then(r => r.data),
  prescriptions: (person: string) => api.get<PrescriptionDto[]>(`/api/insights/${encodeURIComponent(person)}/prescriptions`).then(r => r.data),
  health: (person: string) => api.get<HealthIndicatorsDto>(`/api/insights/${encodeURIComponent(person)}/health`).then(r => r.data),
  trajectory: (person: string, period = 90) => api.get<TrajectoryResultDto>(`/api/insights/${encodeURIComponent(person)}/trajectory?period=${period}`).then(r => r.data),
  keySignals: (person: string) => api.get<KeySignalsResultDto>(`/api/insights/${encodeURIComponent(person)}/key-signals`).then(r => r.data),
  strengthsChallenges: (person: string) => api.get<StrengthsChallengesResultDto>(`/api/insights/${encodeURIComponent(person)}/strengths-challenges`).then(r => r.data),
  crossProfile: (person: string) => api.get<CrossProfileResultDto>(`/api/insights/${encodeURIComponent(person)}/cross-profile`).then(r => r.data),
  checkIn: (person: string, data: CheckInRequest) => api.post<CheckInResponse>(`/api/insights/${encodeURIComponent(person)}/checkin`, data).then(r => r.data),
};
