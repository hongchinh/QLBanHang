import { ChevronDown, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { NavGroup } from '../nav-config';
import type { MatchRange } from './nav-search';
import { SidebarItemList } from './sidebar-item';

interface SidebarGroupProps {
  group: NavGroup;
  open: boolean;
  onToggle: () => void;
  activePath: string | null;
  listId: string;
  // Set while searching: the header becomes a static label with the result count.
  resultCount?: number;
  matches?: ReadonlyMap<string, MatchRange>;
}

export function SidebarGroup({ group, open, onToggle, activePath, listId, resultCount, matches }: SidebarGroupProps) {
  const { icon: Icon, label, color, items } = group;
  const Chevron = open ? ChevronDown : ChevronRight;
  const iconBlock = (
    <span
      aria-hidden
      className={cn('flex h-[26px] w-[26px] shrink-0 items-center justify-center rounded-md text-white', color)}
    >
      <Icon className="h-4 w-4" />
    </span>
  );
  return (
    <section className="mt-2">
      {resultCount !== undefined ? (
        <div className="flex w-full items-center gap-2 rounded-md bg-nav-group-bg px-2 py-1.5 text-sm font-semibold text-foreground">
          {iconBlock}
          <span className="flex-1 truncate text-left">{label}</span>
          <span className="text-xs tabular-nums text-muted-foreground">{resultCount}</span>
        </div>
      ) : (
        <button
          type="button"
          aria-expanded={open}
          aria-controls={listId}
          onClick={onToggle}
          className="flex w-full items-center gap-2 rounded-md bg-nav-group-bg px-2 py-1.5 text-sm font-semibold text-foreground hover:bg-accent"
        >
          {iconBlock}
          <span className="flex-1 truncate text-left">{label}</span>
          <Chevron className="h-4 w-4 text-muted-foreground" />
        </button>
      )}
      {/* The list always exists so aria-controls resolves; `hidden` takes it out of the tab order. */}
      <SidebarItemList
        id={listId}
        hidden={!open}
        label={label}
        items={items}
        activePath={activePath}
        matches={matches}
      />
    </section>
  );
}
