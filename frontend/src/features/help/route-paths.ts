import { isValidElement } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';

// Pages that are not screens of the app: no help file, no HELP_PENDING entry.
export const ROUTES_WITHOUT_HELP: readonly string[] = ['/login', '/403', '/404'];

function join(parent: string, path: string): string {
  if (path.startsWith('/')) return path;
  return `${parent === '/' ? '' : parent}/${path}`;
}

// Leaf routes as full paths. An index route takes its parent's path; a layout route (no path) adds nothing;
// redirects (<Navigate>), '*' and ROUTES_WITHOUT_HELP are left out.
export function flattenRoutePaths(routes: RouteObject[]): string[] {
  const out = new Set<string>();
  const walk = (list: RouteObject[], parent: string) => {
    for (const r of list) {
      const full = r.index ? parent || '/' : r.path === undefined ? parent : join(parent, r.path);
      const redirect = isValidElement(r.element) && r.element.type === Navigate;
      const leaf = !r.children?.length && (r.index || r.path !== undefined);
      if (leaf && r.path !== '*' && !redirect && !ROUTES_WITHOUT_HELP.includes(full)) out.add(full);
      if (r.children) walk(r.children, full);
    }
  };
  walk(routes, '');
  return [...out].sort();
}
