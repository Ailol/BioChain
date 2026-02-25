import axios from 'axios';
import keycloak from '@/auth/keycloak';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '',
});

api.interceptors.request.use((config) => {
  if (import.meta.env.VITE_DISABLE_AUTH === 'true') return config;
  if (keycloak.token) {
    config.headers.Authorization = `Bearer ${keycloak.token}`;
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  async (err) => {
    if (err.response?.status === 401 && import.meta.env.VITE_DISABLE_AUTH !== 'true') {
      try {
        await keycloak.updateToken(30);
        err.config.headers.Authorization = `Bearer ${keycloak.token}`;
        return axios(err.config);
      } catch {
        keycloak.login();
      }
    }
    return Promise.reject(err);
  }
);

export default api;
