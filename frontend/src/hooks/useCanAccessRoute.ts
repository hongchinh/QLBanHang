import { canAccessRouteWith } from '@/lib/route-permissions';
import { useAuthStore } from '@/stores/auth-store';

// Whether the signed-in user may open a route (same rules as ProtectedRoute); links to other screens
// render as plain text when it is false instead of landing on /403.
export function useCanAccessRoute(): (pathname: string) => boolean {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const isInRole = useAuthStore((s) => s.isInRole);
  return (pathname) => canAccessRouteWith(pathname, hasPermission, isInRole);
}
