import { matchRoutes, type RouteObject } from 'react-router-dom';
import type { HelpDoc } from './types';

export interface HelpIndex {
  docs: readonly HelpDoc[];
  byKey: ReadonlyMap<string, HelpDoc>;
  resolve(pathname: string): HelpDoc | null;
}

export function buildHelpIndex(docs: readonly HelpDoc[]): HelpIndex {
  const sorted = [...docs].sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));
  const byKey = new Map(sorted.map((d) => [d.key, d]));
  const routes: RouteObject[] = sorted.flatMap((d) =>
    d.routes.map((path) => ({ path, id: `${d.key}|${path}` })),
  );

  return {
    docs: sorted,
    byKey,
    resolve(pathname) {
      const matches = matchRoutes(routes, pathname);
      const id = matches?.[matches.length - 1]?.route.id;
      if (!id) return null;
      return byKey.get(id.slice(0, id.indexOf('|'))) ?? null;
    },
  };
}

export function firstStaticPath(doc: HelpDoc): string {
  return doc.routes[0].replace(/:[^/]+/g, '_');
}
