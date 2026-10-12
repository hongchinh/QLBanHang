import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { cn } from '@/lib/utils';
import type { NavGroup, NavItem } from '../nav-config';
import { SidebarFlyout } from './sidebar-flyout';
import { SidebarItem, SidebarItemList, type PinControls } from './sidebar-item';

interface SidebarRailProps {
  dashboardItem: NavItem;
  groups: NavGroup[];
  activePath: string | null;
  activeGroupId: string | null;
  pins?: PinControls;
}

export function SidebarRail({ dashboardItem, groups, activePath, activeGroupId, pins }: SidebarRailProps) {
  const { pathname } = useLocation();
  const [openId, setOpenId] = useState<string | null>(null); // one flyout at a time
  useEffect(() => setOpenId(null), [pathname]);
  const close = () => setOpenId(null);

  return (
    <nav aria-label="Chức năng" className="flex min-h-0 flex-1 flex-col items-center gap-1 overflow-y-auto overflow-x-hidden py-3">
      <div className="my-1 h-px w-8 bg-border" />
      <SidebarItem item={dashboardItem} active={dashboardItem.to === activePath} collapsed />
      {groups.map((group) => (
        <div key={group.id} className="relative flex w-full justify-center py-0.5">
          {group.id === activeGroupId && (
            <span aria-hidden className="absolute bottom-1 left-0 top-1 w-[3px] rounded-r bg-primary" />
          )}
          <SidebarFlyout
            title={group.label}
            icon={group.icon}
            iconClassName={cn(group.color, 'text-white')}
            open={openId === group.id}
            onOpenChange={(open) => setOpenId(open ? group.id : null)}
            trigger={
              <button
                type="button"
                aria-label={group.label}
                data-active={group.id === activeGroupId ? 'true' : undefined}
                className={cn(
                  'flex h-[34px] w-[34px] items-center justify-center rounded-md text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  group.color,
                  group.id === activeGroupId && 'ring-2 ring-primary ring-offset-2 ring-offset-card',
                )}
              >
                <group.icon className="h-4 w-4" />
              </button>
            }
          >
            <SidebarItemList label={group.label} items={group.items} activePath={activePath} pins={pins} onNavigate={close} />
          </SidebarFlyout>
        </div>
      ))}
    </nav>
  );
}
