import { useId, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useUiStore } from '@/stores/ui-store';
import type { NavGroup, NavItem } from '../nav-config';
import { findActiveItem } from './nav-search';
import { SidebarGroup } from './sidebar-group';
import { SidebarItem } from './sidebar-item';

interface SidebarProps {
  dashboardItem: NavItem;
  groups: NavGroup[];
  collapsed: boolean;
  onClose?: () => void;
}

export function Sidebar({ dashboardItem, groups, collapsed, onClose }: SidebarProps) {
  const { pathname } = useLocation();
  const activeItem = useMemo(
    () => findActiveItem(pathname, [dashboardItem, ...groups.flatMap((g) => g.items)]),
    [pathname, dashboardItem, groups],
  );
  const activePath = activeItem?.to ?? null;

  const idPrefix = useId();
  const sidebarGroupOpen = useUiStore((s) => s.sidebarGroupOpen);
  const setSidebarGroupOpen = useUiStore((s) => s.setSidebarGroupOpen);
  const activeGroupId = activeItem ? (groups.find((g) => g.items.includes(activeItem))?.id ?? null) : null;
  // The group of the current page opens temporarily on every route change (never stored).
  // A header click on it records the choice and drops the temporary state until the next route change.
  const [tempOpen, setTempOpen] = useState<{ pathname: string; groupId: string | null } | null>(null);
  const [seenPath, setSeenPath] = useState(pathname);
  if (seenPath !== pathname) {
    // Route change: forget the earlier click so the active group opens again (adjusted during render, no effect).
    setSeenPath(pathname);
    setTempOpen(null);
  }
  const tempGroupId = tempOpen?.pathname === pathname ? tempOpen.groupId : activeGroupId;
  const isOpen = (g: NavGroup) => g.id === tempGroupId || (sidebarGroupOpen[g.id] ?? g.defaultOpen);
  const toggleGroup = (g: NavGroup) => {
    setSidebarGroupOpen(g.id, !isOpen(g));
    if (g.id === tempGroupId) setTempOpen({ pathname, groupId: null });
  };

  return (
    <>
      {onClose && (
        <div className="flex h-16 items-center justify-end border-b px-3">
          <Button variant="ghost" size="icon" aria-label="Đóng menu" onClick={onClose}>
            <X className="h-5 w-5" />
          </Button>
        </div>
      )}
      {collapsed ? (
        <nav className="flex-1 overflow-y-auto overflow-x-hidden px-2 py-3">
          <div className="space-y-1">
            <SidebarItem item={dashboardItem} active={dashboardItem.to === activePath} collapsed />
          </div>
          {groups.map((group) => (
            <div key={group.id} className="mt-4 space-y-1">
              {group.items.map((item) => (
                <SidebarItem key={item.to} item={item} active={item.to === activePath} collapsed />
              ))}
            </div>
          ))}
        </nav>
      ) : (
        <nav aria-label="Chức năng" className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden p-3">
          <ul>
            <SidebarItem item={dashboardItem} active={dashboardItem.to === activePath} />
          </ul>
          {groups.map((g) => (
            <SidebarGroup
              key={g.id}
              group={g}
              open={isOpen(g)}
              onToggle={() => toggleGroup(g)}
              activePath={activePath}
              listId={`${idPrefix}-group-${g.id}`}
            />
          ))}
        </nav>
      )}
    </>
  );
}
