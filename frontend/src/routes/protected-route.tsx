import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import type { Permission } from '@/lib/permissions';

interface Props {
  children: React.ReactNode;
  permission?: Permission;
  // Accessible when the user has at least one of these (the route rule's anyPermission).
  anyPermission?: Permission[];
  requireRole?: string;
}

export function ProtectedRoute({ children, permission, anyPermission, requireRole }: Props) {
  const location = useLocation();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated());
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const isInRole = useAuthStore((s) => s.isInRole);

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }
  if (permission && !hasPermission(permission)) {
    return <Navigate to="/403" replace />;
  }
  if (anyPermission && !anyPermission.some((p) => hasPermission(p))) {
    return <Navigate to="/403" replace />;
  }
  if (requireRole && !isInRole(requireRole)) {
    return <Navigate to="/403" replace />;
  }
  return <>{children}</>;
}
