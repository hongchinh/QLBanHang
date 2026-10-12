import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';
import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { NavGroup, NavItem } from '../nav-config';
import { findActiveItem } from './nav-search';
import { SidebarItem, SidebarItemList } from './sidebar-item';

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

  return (
    <>
      {onClose && (
        <div className="flex h-16 items-center justify-end border-b px-3">
          <Button variant="ghost" size="icon" aria-label="Đóng menu" onClick={onClose}>
            <X className="h-5 w-5" />
          </Button>
        </div>
      )}
      <nav
        className={cn(
          'flex-1 overflow-y-auto overflow-x-hidden',
          collapsed ? 'px-2 py-3' : 'p-3',
        )}
      >
        {collapsed ? (
          <>
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
          </>
        ) : (
          <>
            <ul className="space-y-1">
              <SidebarItem item={dashboardItem} active={dashboardItem.to === activePath} />
            </ul>
            {groups.map((group) => (
              <div key={group.id} className="mt-4 space-y-1">
                <div className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                  {group.label}
                </div>
                <SidebarItemList label={group.label} items={group.items} activePath={activePath} />
              </div>
            ))}
          </>
        )}
      </nav>
    </>
  );
}
