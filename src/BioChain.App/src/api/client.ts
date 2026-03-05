import axios from 'axios';
import keycloak from '@/auth/keycloak';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '',
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use(async (config) => {
  if (import.meta.env.VITE_DISABLE_AUTH === 'true') {
    config.headers['X-Dev-User'] = 'dev-user';
    config.headers['X-Dev-Email'] = 'dev@local';
    return config;
  }

  if (keycloak.authenticated && keycloak.token) {
    // Refresh if expiring within 30s
    try {
      await keycloak.updateToken(30);
    } catch {
      keycloak.login();
      return Promise.reject(new Error('Token refresh failed'));
    }
    config.headers.Authorization = `Bearer ${keycloak.token}`;
  }

  return config;
});

export default api;
