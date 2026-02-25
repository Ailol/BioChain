import { useNavigate } from 'react-router-dom';
import { ShieldOff } from 'lucide-react';

export default function UnauthorizedPage() {
  const navigate = useNavigate();

  return (
    <div className="flex items-center justify-center min-h-screen bg-bg-primary">
      <div className="text-center">
        <ShieldOff className="w-12 h-12 text-accent-danger mx-auto mb-4" />
        <h1 className="text-xl font-semibold text-text-primary mb-2">Unauthorized</h1>
        <p className="text-sm text-text-secondary mb-6">You don't have permission to access this page.</p>
        <button
          onClick={() => navigate('/')}
          className="px-4 py-2 text-sm font-medium rounded-lg bg-accent-primary hover:bg-accent-primary/80 text-white"
        >
          Go home
        </button>
      </div>
    </div>
  );
}
