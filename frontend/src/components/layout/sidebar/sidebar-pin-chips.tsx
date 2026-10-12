import { useState } from 'react';
import { X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import type { NavItem } from '../nav-config';

const MAX_CHIPS = 6;

interface SidebarPinChipsProps {
  items: NavItem[];
  activePath: string | null;
  onUnpin: (path: string) => void;
}

export function SidebarPinChips({ items, activePath, onUnpin }: SidebarPinChipsProps) {
  // Not persisted: every page load starts collapsed.
  const [expanded, setExpanded] = useState(false);
  const hiddenCount = items.length - MAX_CHIPS;
  const shown = expanded ? items : items.slice(0, MAX_CHIPS);

  return (
    <div className="mb-2 border-b border-dashed pb-2">
      <ul aria-label="Mục đã ghim" className="flex flex-wrap gap-1">
        {shown.map((item) => {
          const active = item.to === activePath;
          return (
            <li
              key={item.to}
              className={cn(
                'group/chip flex max-w-[120px] items-center rounded-full border text-xs',
                active
                  ? 'border-primary bg-primary text-primary-foreground'
                  : 'border-nav-pin-border bg-nav-pin-bg text-nav-pin-fg',
              )}
            >
              <Link
                to={item.to}
                title={item.label}
                aria-current={active ? 'page' : undefined}
                className="truncate rounded-full py-0.5 pl-2 pr-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {item.label}
              </Link>
              <button
                type="button"
                aria-label={`Bỏ ghim ${item.label}`}
                onClick={() => onUnpin(item.to)}
                className="mr-1 shrink-0 rounded-full opacity-0 group-hover/chip:opacity-100 group-focus-within/chip:opacity-100 focus-visible:opacity-100 [@media(hover:none)]:opacity-100"
              >
                <X className="h-3 w-3" aria-hidden />
              </button>
            </li>
          );
        })}
        {hiddenCount > 0 && (
          <li>
            <button
              type="button"
              aria-expanded={expanded}
              onClick={() => setExpanded((e) => !e)}
              className="rounded-full border border-dashed px-2 py-0.5 text-xs text-muted-foreground"
            >
              {expanded ? 'Thu gọn' : `+${hiddenCount}`}
            </button>
          </li>
        )}
      </ul>
    </div>
  );
}
