import { Link } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { NavItem } from '../nav-config';
import type { MatchRange } from './nav-search';

export interface PinControls {
  pinned: ReadonlySet<string>;
  toggle: (path: string) => void;
}

interface SidebarItemProps {
  item: NavItem;
  active: boolean;
  collapsed?: boolean;
  match?: MatchRange;
  pins?: PinControls;
  onNavigate?: () => void;
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

export function SidebarItem({ item, active, collapsed = false, match, pins, onNavigate }: SidebarItemProps) {
  const { to, label } = item;
  const pinned = pins?.pinned.has(to) ?? false;
  const link = (
    <Link
      to={to}
      onClick={onNavigate}
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
      <AppIcon name={item.icon} />
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
  return (
    <li>
      <div className="group/item flex items-center">
        {link}
        {pins && (
          <button
            type="button"
            aria-label={`Ghim ${label}`}
            aria-pressed={pinned}
            onClick={() => pins.toggle(to)}
            className={cn(
              'ml-0.5 shrink-0 rounded p-1 text-muted-foreground hover:text-foreground focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              pinned
                ? 'text-amber-500 opacity-100'
                : 'opacity-0 group-hover/item:opacity-100 [@media(hover:none)]:opacity-60',
            )}
          >
            <AppIcon name="pin" size={14} className={cn(pinned && 'fill-current')} />
          </button>
        )}
      </div>
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
  pins?: PinControls;
  onNavigate?: () => void;
  showSections?: boolean;
}

export function SidebarItemList({
  label,
  items,
  activePath,
  id,
  hidden,
  matches,
  pins,
  onNavigate,
  showSections = true,
}: SidebarItemListProps) {
  return (
    <ul aria-label={label} id={id} hidden={hidden} className="space-y-0.5 pl-2">
      {items.map((item, index) => (
        <SectionedItem
          key={item.to}
          item={item}
          showHeading={showSections && item.section !== undefined && item.section !== items[index - 1]?.section}
          active={item.to === activePath}
          match={matches?.get(item.to)}
          pins={pins}
          onNavigate={onNavigate}
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
  pins?: PinControls;
  onNavigate?: () => void;
}

function SectionedItem({ item, showHeading, active, match, pins, onNavigate }: SectionedItemProps) {
  return (
    <>
      {showHeading && (
        <li role="presentation">
          <h4 className="px-3 pb-0.5 pt-2 text-[11px] font-semibold text-muted-foreground">{item.section}</h4>
        </li>
      )}
      <SidebarItem item={item} active={active} match={match} pins={pins} onNavigate={onNavigate} />
    </>
  );
}
