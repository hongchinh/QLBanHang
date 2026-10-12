import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { SearchX, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useUiStore } from '@/stores/ui-store';
import type { NavGroup, NavItem } from '../nav-config';
import { filterNavGroups, findActiveItem } from './nav-search';
import { SidebarGroup } from './sidebar-group';
import { SidebarItem } from './sidebar-item';
import { SidebarSearch } from './sidebar-search';

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

  const navigate = useNavigate();
  const navId = `${idPrefix}-nav`;
  const inputRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState('');
  const searching = query.trim() !== '';
  const filtered = useMemo(() => (searching ? filterNavGroups(groups, query) : null), [searching, groups, query]);

  // The box clears on navigation and when the sidebar collapses.
  useEffect(() => {
    setQuery('');
  }, [pathname, collapsed]);

  const clearSearch = useCallback(() => {
    setQuery('');
    inputRef.current?.focus();
  }, []);
  const openFirstResult = () => {
    const first = filtered?.groups[0]?.items[0];
    if (first) navigate(first.to);
  };
  const navRef = useRef<HTMLElement>(null);
  const focusFirstResult = () => {
    navRef.current?.querySelector<HTMLAnchorElement>('a[href]')?.focus();
  };

  // A native listener (a React onKeyDown on <nav> trips jsx-a11y/no-noninteractive-element-interactions).
  useEffect(() => {
    const nav = navRef.current;
    if (!nav || !searching) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        clearSearch();
        return;
      }
      const links = Array.from(nav.querySelectorAll<HTMLAnchorElement>('a[href]'));
      const index = links.indexOf(document.activeElement as HTMLAnchorElement);
      if (index < 0) return;
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        links[index + 1]?.focus();
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        (index === 0 ? inputRef.current : links[index - 1])?.focus();
      }
    };
    nav.addEventListener('keydown', onKeyDown);
    return () => nav.removeEventListener('keydown', onKeyDown);
  }, [searching, clearSearch]);

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
        <>
          <SidebarSearch
            value={query}
            onChange={setQuery}
            onEnter={openFirstResult}
            onArrowDown={focusFirstResult}
            onClear={clearSearch}
            resultCount={filtered?.count ?? null}
            inputRef={inputRef}
            controlsId={navId}
          />
          <nav ref={navRef} id={navId} aria-label="Chức năng" className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden p-3">
            {filtered ? (
              filtered.count === 0 ? (
                <div className="flex flex-col items-center gap-1 py-8 text-center text-sm text-muted-foreground">
                  <SearchX className="h-6 w-6" aria-hidden />
                  <p>Không tìm thấy chức năng</p>
                  <p className="text-xs">Thử từ khóa ngắn hơn</p>
                </div>
              ) : (
                filtered.groups.map((g) => (
                  <SidebarGroup
                    key={g.id}
                    group={g}
                    open
                    onToggle={() => {}}
                    resultCount={g.items.length}
                    matches={filtered.matches}
                    activePath={activePath}
                    listId={`${idPrefix}-group-${g.id}`}
                  />
                ))
              )
            ) : (
              <>
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
              </>
            )}
          </nav>
        </>
      )}
    </>
  );
}
