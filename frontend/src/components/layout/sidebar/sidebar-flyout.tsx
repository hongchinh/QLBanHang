import { useRef } from 'react';
import type * as React from 'react';
import { AppIcon, type IconName } from '@/components/icons';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

interface SidebarFlyoutProps {
  title: string;
  icon: IconName;
  // Background + text classes of the title icon block.
  iconClassName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  trigger: React.ReactElement;
  children: React.ReactNode;
}

export function SidebarFlyout({ title, icon, iconClassName, open, onOpenChange, trigger, children }: SidebarFlyoutProps) {
  const contentRef = useRef<HTMLDivElement>(null);
  // Radix skips links when choosing the initial focus, which would land on the first star (pin) button.
  const focusFirstLink = (event: Event) => {
    event.preventDefault();
    const content = contentRef.current;
    (content?.querySelector<HTMLElement>('a[href]') ?? content)?.focus();
  };

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <Tooltip>
        <TooltipTrigger asChild>
          <PopoverTrigger asChild>{trigger}</PopoverTrigger>
        </TooltipTrigger>
        <TooltipContent side="right">{title}</TooltipContent>
      </Tooltip>
      <PopoverContent
        ref={contentRef}
        aria-label={title}
        onOpenAutoFocus={focusFirstLink}
        side="right"
        align="start"
        collisionPadding={8}
        className="max-h-[var(--radix-popover-content-available-height)] w-60 overflow-y-auto p-2"
      >
        <div className="mb-1 flex items-center gap-2 px-1 py-1 text-sm font-semibold">
          <span aria-hidden className={cn('flex h-[26px] w-[26px] items-center justify-center rounded-md', iconClassName)}>
            <AppIcon name={icon} />
          </span>
          {title}
        </div>
        {children}
      </PopoverContent>
    </Popover>
  );
}
