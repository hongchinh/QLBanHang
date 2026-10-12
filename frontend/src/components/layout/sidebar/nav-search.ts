import type { NavItem } from '../nav-config';

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
