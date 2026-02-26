import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '@/stores/authStore';
import { hasAnyRole } from '@/utils/roles';

interface Props {
  requiredRoles?: string[];
}

export function ProtectedRoute({ requiredRoles }: Props) {
  const { isAuthenticated, effectiveRoles, hasSelectedRole } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!hasSelectedRole || effectiveRoles.length === 0) {
    return <Navigate to="/select-role" replace />;
  }

  if (requiredRoles && !hasAnyRole(effectiveRoles, requiredRoles)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <Outlet />;
}
