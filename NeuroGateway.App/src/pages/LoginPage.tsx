import { useEffect } from 'react';
import keycloak from '@/auth/keycloak';

export default function LoginPage() {
  useEffect(() => {
    if (import.meta.env.VITE_DISABLE_AUTH !== 'true') {
      keycloak.login();
    }
  }, []);

  return (
    <div className="flex items-center justify-center min-h-screen bg-bg-primary">
      <div className="text-center">
        <div className="text-2xl font-semibold mb-2">
          <span className="text-accent-primary">Neuro</span>React
        </div>
        <p className="text-sm text-text-secondary">Redirecting to login...</p>
      </div>
    </div>
  );
}
