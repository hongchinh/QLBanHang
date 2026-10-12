import type { NavGroup, NavItem } from '../nav-config';

function matchesPath(pathname: string, to: string): boolean {
  // '/' is a prefix of every path, so it only matches itself.
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}

// The item whose `to` is the longest segment-boundary prefix of the path,
// so /settings/branches lights "Cấu hình hệ thống" and /settings/my-quotation-settings
// lights only "Cài đặt của tôi".
export function findActiveItem(pathname: string, items: readonly NavItem[]): NavItem | null {
  let best: NavItem | null = null;
  for (const item of items) {
    if (matchesPath(pathname, item.to) && (!best || item.to.length > best.to.length)) best = item;
  }
  return best;
}

export interface MatchRange {
  start: number;
  end: number;
}

export interface NavFilterResult {
  groups: NavGroup[];
  // Keyed by item `to`; absent when the item matched through its group or section.
  matches: Map<string, MatchRange>;
  count: number;
}

// Each character of the NFC input maps to one output character, so an index in the
// result is an index in the (NFC) label and can be used to highlight it.
export function normalizeVi(text: string): string {
  return Array.from(text.normalize('NFC'), (ch) =>
    ch.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[đĐ]/g, 'd').toLowerCase(),
  ).join('');
}

const countItems = (groups: readonly NavGroup[]) => groups.reduce((n, g) => n + g.items.length, 0);

export function filterNavGroups(groups: readonly NavGroup[], query: string): NavFilterResult {
  const q = normalizeVi(query.trim());
  const matches = new Map<string, MatchRange>();
  if (!q) return { groups: [...groups], matches, count: countItems(groups) };

  const result: NavGroup[] = [];
  for (const group of groups) {
    const groupHit = normalizeVi(group.label).includes(q);
    const items = group.items.filter((item: NavItem) => {
      const start = normalizeVi(item.label).indexOf(q);
      if (start >= 0) {
        matches.set(item.to, { start, end: start + q.length });
        return true;
      }
      return groupHit || (item.section !== undefined && normalizeVi(item.section).includes(q));
    });
    if (items.length > 0) result.push({ ...group, items });
  }
  return { groups: result, matches, count: countItems(result) };
}

// Items for the stored pin paths, in pin order. Paths the user can no longer see
// (lost permission, removed route) are skipped here but stay in the store.
export function resolvePinnedItems(paths: readonly string[], groups: readonly NavGroup[]): NavItem[] {
  const byPath = new Map(groups.flatMap((g) => g.items.map((i) => [i.to, i] as const)));
  return paths.flatMap((p) => byPath.get(p) ?? []);
}
