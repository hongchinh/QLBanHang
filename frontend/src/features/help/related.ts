import type { NavGroup } from '@/components/layout/nav-config';
import { findActiveItem } from '@/components/layout/sidebar/nav-search';
import { firstStaticPath, type HelpIndex } from './registry';
import type { HelpDoc } from './types';

export function accessibleRelated(
  doc: HelpDoc,
  index: HelpIndex,
  canAccess: (path: string) => boolean,
): HelpDoc[] {
  return doc.related
    .map((key) => index.byKey.get(key))
    .filter((d): d is HelpDoc => d !== undefined && canAccess(firstStaticPath(d)));
}

export function docsInSameGroup(
  pathname: string,
  groups: readonly NavGroup[],
  index: HelpIndex,
  canAccess: (path: string) => boolean,
): HelpDoc[] {
  const group = groups.find((g) => findActiveItem(pathname, g.items) !== null);
  if (!group) return [];
  return index.docs
    .filter((d) => {
      const path = firstStaticPath(d);
      return findActiveItem(path, group.items) !== null && canAccess(path);
    })
    .sort((a, b) => a.title.localeCompare(b.title, 'vi'));
}
