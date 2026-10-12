import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Search, Star } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
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
  pinnedItems: NavItem[];
  onSearch: () => void;
}

export function SidebarRail({ dashboardItem, groups, activePath, activeGroupId, pins, pinnedItems, onSearch }: SidebarRailProps) {
  const { pathname } = useLocation();
  const [openId, setOpenId] = useState<string | null>(null); // one flyout at a time
  useEffect(() => setOpenId(null), [pathname]);
  const close = () => setOpenId(null);
  const searchRef = useRef<HTMLButtonElement>(null);

  // Unpinning inside the star flyout unmounts the clicked button: hand focus to the neighbouring pin's link,
  // or, when it was the last pin (the whole flyout and its trigger go away), to the search button.
  // The target comes from pinnedItems, not document.activeElement: Safari and macOS Firefox do not focus a
  // button on mouse click.
  const pinListRef = useRef<HTMLDivElement>(null);
  const flyoutPins = useMemo<PinControls | undefined>(() => {
    if (!pins) return undefined;
    return {
      pinned: pins.pinned,
      toggle: (path) => {
        const index = pinnedItems.findIndex((item) => item.to === path);
        if (pins.pinned.has(path) && index >= 0) {
          if (pinnedItems.length === 1) {
            setOpenId(null);
            searchRef.current?.focus();
          } else {
            const neighbour = index + 1 < pinnedItems.length ? index + 1 : index - 1;
            // The list renders pinnedItems in order, one link per row, and still shows every row at this point.
            pinListRef.current?.querySelectorAll<HTMLElement>('a[href]')[neighbour]?.focus();
          }
        }
        pins.toggle(path);
      },
    };
  }, [pins, pinnedItems]);

  return (
    <nav aria-label="Chức năng" className="flex min-h-0 flex-1 flex-col items-center gap-1 overflow-y-auto overflow-x-hidden py-3">
      <Tooltip>
        <TooltipTrigger asChild>
          <button
            ref={searchRef}
            type="button"
            aria-label="Tìm chức năng"
            onClick={onSearch}
            className="flex h-10 w-10 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <Search className="h-4 w-4" />
          </button>
        </TooltipTrigger>
        <TooltipContent side="right">Tìm chức năng</TooltipContent>
      </Tooltip>
      {pins && pinnedItems.length > 0 && (
        <SidebarFlyout
          title="Mục đã ghim"
          icon={Star}
          iconClassName="bg-nav-pin-bg text-nav-pin-fg"
          open={openId === 'pins'}
          onOpenChange={(open) => setOpenId(open ? 'pins' : null)}
          trigger={
            <button
              type="button"
              aria-label="Mục đã ghim"
              className="flex h-10 w-10 items-center justify-center rounded-md text-nav-pin-fg hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <Star className="h-4 w-4 fill-current" />
            </button>
          }
        >
          <div ref={pinListRef}>
            <SidebarItemList
              label="Mục đã ghim"
              items={pinnedItems}
              activePath={activePath}
              pins={flyoutPins}
              onNavigate={close}
              showSections={false}
            />
          </div>
        </SidebarFlyout>
      )}
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
                  'flex h-[34px] w-[34px] items-center justify-center rounded-md text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-card',
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
