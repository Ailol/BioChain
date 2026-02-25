import { LogOut, Menu } from 'lucide-react';
import { useAuthStore } from '@/stores/authStore';
import { PersonSelector } from './PersonSelector';
import keycloak from '@/auth/keycloak';

interface Props {
  onMenuToggle: () => void;
}

export function Topbar({ onMenuToggle }: Props) {
  const { email } = useAuthStore();

  const handleLogout = () => {
    if (import.meta.env.VITE_DISABLE_AUTH === 'true') return;
    keycloak.logout();
  };

  return (
    <header className="h-14 border-b border-white/5 bg-bg-secondary flex items-center justify-between px-4 shrink-0">
      <div className="flex items-center gap-3">
        <button onClick={onMenuToggle} className="lg:hidden text-text-muted hover:text-text-primary">
          <Menu className="w-5 h-5" />
        </button>
        <div className="text-base font-semibold tracking-tight">
          <span className="text-accent-primary">Neuro</span>
          <span className="text-text-primary">React</span>
        </div>
      </div>

      <div className="flex items-center gap-4">
        <PersonSelector />
        <div className="flex items-center gap-2">
          {email && <span className="text-xs text-text-muted hidden sm:block">{email}</span>}
          <button
            onClick={handleLogout}
            className="p-1.5 rounded-md text-text-muted hover:text-text-primary hover:bg-bg-hover transition-colors"
            title="Logout"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </div>
    </header>
  );
}
