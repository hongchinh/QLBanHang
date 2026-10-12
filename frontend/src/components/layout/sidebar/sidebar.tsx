import { NavLink } from 'react-router-dom';
import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { NavGroup, NavItem } from '../nav-config';

interface SidebarProps {
  dashboardItem: NavItem;
  groups: NavGroup[];
  collapsed: boolean;
  onClose?: () => void;
}

export function Sidebar({ dashboardItem, groups, collapsed, onClose }: SidebarProps) {
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
        <div className="space-y-1">
          <SidebarLink item={dashboardItem} collapsed={collapsed} />
        </div>
        {groups.map((group) => (
          <div key={group.id} className="mt-4 space-y-1">
            {!collapsed && (
              <div className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                {group.label}
              </div>
            )}
            {group.items.map((item) => (
              <SidebarLink key={item.to} item={item} collapsed={collapsed} />
            ))}
          </div>
        ))}
      </nav>
    </>
  );
}

interface SidebarLinkProps {
  item: NavItem;
  collapsed: boolean;
}

function SidebarLink({ item, collapsed }: SidebarLinkProps) {
  const { to, label, icon: Icon } = item;
  const link = (
    <NavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) =>
        cn(
          'flex items-center rounded-md text-sm font-medium transition-colors',
          collapsed ? 'h-10 w-10 justify-center mx-auto' : 'gap-3 px-3 py-2',
          isActive
            ? 'bg-primary text-primary-foreground'
            : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
        )
      }
      aria-label={collapsed ? label : undefined}
    >
      <Icon className="h-4 w-4 shrink-0" />
      {!collapsed && <span className="truncate">{label}</span>}
    </NavLink>
  );

  if (!collapsed) return link;
  return (
    <Tooltip>
      <TooltipTrigger asChild>{link}</TooltipTrigger>
      <TooltipContent side="right">{label}</TooltipContent>
    </Tooltip>
  );
}
