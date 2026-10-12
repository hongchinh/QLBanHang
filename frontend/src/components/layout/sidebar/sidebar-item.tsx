import { Link } from 'react-router-dom';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { NavItem } from '../nav-config';
import type { MatchRange } from './nav-search';

interface SidebarItemProps {
  item: NavItem;
  active: boolean;
  collapsed?: boolean;
  match?: MatchRange;
}

function Highlight({ text, match }: { text: string; match?: MatchRange }) {
  if (!match) return <>{text}</>;
  const nfc = text.normalize('NFC');
  return (
    <>
      {nfc.slice(0, match.start)}
      <mark className="rounded-sm bg-nav-match text-inherit">{nfc.slice(match.start, match.end)}</mark>
      {nfc.slice(match.end)}
    </>
  );
}

export function SidebarItem({ item, active, collapsed = false, match }: SidebarItemProps) {
  const { to, label, icon: Icon } = item;
  const link = (
    <Link
      to={to}
      aria-current={active ? 'page' : undefined}
      aria-label={collapsed ? label : undefined}
      className={cn(
        'flex items-center rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        collapsed ? 'mx-auto h-10 w-10 justify-center' : 'min-w-0 flex-1 gap-3 px-3 py-2',
        active
          ? 'bg-primary text-primary-foreground'
          : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
      )}
    >
      <Icon className="h-4 w-4 shrink-0" />
      {!collapsed && <span className="truncate"><Highlight text={label} match={match} /></span>}
    </Link>
  );

  if (collapsed) {
    return (
      <Tooltip>
        <TooltipTrigger asChild>{link}</TooltipTrigger>
        <TooltipContent side="right">{label}</TooltipContent>
      </Tooltip>
    );
  }
  // The row leaves room for the star button Phase 04 puts next to the link.
  return (
    <li>
      <div className="flex items-center">{link}</div>
    </li>
  );
}

interface SidebarItemListProps {
  label: string;
  items: readonly NavItem[];
  activePath: string | null;
  id?: string;
  hidden?: boolean;
  matches?: ReadonlyMap<string, MatchRange>;
}

export function SidebarItemList({ label, items, activePath, id, hidden, matches }: SidebarItemListProps) {
  return (
    <ul aria-label={label} id={id} hidden={hidden} className="space-y-0.5 pl-2">
      {items.map((item, index) => (
        <SectionedItem
          key={item.to}
          item={item}
          showHeading={item.section !== undefined && item.section !== items[index - 1]?.section}
          active={item.to === activePath}
          match={matches?.get(item.to)}
        />
      ))}
    </ul>
  );
}

interface SectionedItemProps {
  item: NavItem;
  showHeading: boolean;
  active: boolean;
  match?: MatchRange;
}

function SectionedItem({ item, showHeading, active, match }: SectionedItemProps) {
  return (
    <>
      {showHeading && (
        <li role="presentation">
          <h4 className="px-3 pb-0.5 pt-2 text-[11px] font-semibold text-muted-foreground">{item.section}</h4>
        </li>
      )}
      <SidebarItem item={item} active={active} match={match} />
    </>
  );
}
