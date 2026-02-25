import { useNavigate } from 'react-router-dom';
import { User, Briefcase, Layers } from 'lucide-react';
import { authApi } from '@/api/auth';
import { useAuthStore } from '@/stores/authStore';

const roles = [
  { role: 'private', icon: User, title: 'Personal', description: 'Track your own biochemistry and personality insights.' },
  { role: 'work', icon: Briefcase, title: 'Professional', description: 'Analyze candidates and team members.' },
  { role: 'both', icon: Layers, title: 'Both', description: 'Personal tracking + professional analysis.' },
] as const;

export default function RoleSelectionPage() {
  const navigate = useNavigate();
  const { setUser } = useAuthStore();

  const handleSelect = async (role: string) => {
    try {
      await authApi.setRole({ role });
      const me = await authApi.getMe();
      setUser(me);
      navigate('/');
    } catch (err) {
      console.error('Failed to set role:', err);
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen bg-bg-primary p-4">
      <div className="max-w-2xl w-full">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-semibold text-text-primary mb-2">Choose your experience</h1>
          <p className="text-sm text-text-secondary">Select how you'll use NeuroReact. You can change this later.</p>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          {roles.map(({ role, icon: Icon, title, description }) => (
            <button
              key={role}
              onClick={() => handleSelect(role)}
              className="flex flex-col items-center gap-3 p-6 rounded-xl bg-bg-card border border-white/5 hover:border-accent-primary/30 hover:bg-bg-hover transition-all text-center group"
            >
              <div className="w-12 h-12 rounded-full bg-accent-primary/10 flex items-center justify-center group-hover:bg-accent-primary/20 transition-colors">
                <Icon className="w-6 h-6 text-accent-primary" />
              </div>
              <div>
                <div className="text-base font-medium text-text-primary mb-1">{title}</div>
                <div className="text-xs text-text-secondary leading-relaxed">{description}</div>
              </div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
