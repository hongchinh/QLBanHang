import { useCanAccessRoute } from './useCanAccessRoute';

// Like useCanAccessRoute, for a link that may carry a query string or a hash: only the path part is checked.
// The route rules match a bare path, so a path with "?..." would match no rule and count as open to everyone.
export function useCanOpenLink(): (to: string) => boolean {
  const canAccessRoute = useCanAccessRoute();
  return (to) => canAccessRoute(to.split(/[?#]/, 1)[0]);
}
