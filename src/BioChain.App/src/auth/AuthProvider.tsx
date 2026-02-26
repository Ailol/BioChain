import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import keycloak from './keycloak';
import { useAuthStore } from '@/stores/authStore';
import { authApi } from '@/api/auth';
import { LoadingSpinner } from '@/components/LoadingSpinner';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const initCalled = useRef(false);
  const navigate = useNavigate();
  const { setUser, setInitialized } = useAuthStore();

  useEffect(() => {
    if (initCalled.current) return;
    initCalled.current = true;

    const init = async () => {
      // Dev bypass
      if (import.meta.env.VITE_DISABLE_AUTH === 'true') {
        setUser({
          userId: 'dev-user',
          email: 'dev@local',
          roles: ['admin'],
          hasSelectedRole: true,
        });
        setInitialized(true);
        setReady(true);
        return;
      }

      try {
        const authenticated = await keycloak.init({
          onLoad: 'login-required',
          checkLoginIframe: false,
        });

        if (!authenticated) {
          keycloak.login();
          return;
        }

        // Sync roles from IdP to DB
        try { await authApi.syncRoles(); } catch { /* first login may fail */ }
        try { await authApi.resolveShares(); } catch { /* optional */ }

        // Get user info
        const me = await authApi.getMe();
        setUser(me);

        // Token refresh
        keycloak.onTokenExpired = () => {
          keycloak.updateToken(30).catch(() => keycloak.login());
        };

        setInitialized(true);

        if (!me.hasSelectedRole || me.roles.length === 0) {
          navigate('/select-role', { replace: true });
        }

        setReady(true);
      } catch (err) {
        console.error('Auth init failed:', err);
        setError(err instanceof Error ? err.message : 'Failed to connect to backend');
        setReady(true);
      }
    };

    init();
  }, []);

  if (!ready) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-bg-primary">
        <LoadingSpinner text="Authenticating..." />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-bg-primary">
        <div className="text-center space-y-3">
          <p className="text-red-400 text-sm">{error}</p>
          <button
            onClick={() => window.location.reload()}
            className="px-4 py-2 bg-accent-primary text-white rounded-lg text-sm hover:opacity-90"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
