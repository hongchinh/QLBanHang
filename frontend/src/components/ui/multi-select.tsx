import * as React from 'react';
import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
  DropdownMenuCheckboxItem,
  DropdownMenuSeparator,
  DropdownMenuItem,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

export interface MultiSelectOption<T extends string> {
  value: T;
  label: string;
}

export interface MultiSelectProps<T extends string> {
  options: ReadonlyArray<MultiSelectOption<T>>;
  value: T[];
  onChange: (next: T[]) => void;
  placeholder: string;
  className?: string;
  triggerClassName?: string;
  ariaLabel?: string;
}

export function MultiSelect<T extends string>({
  options,
  value,
  onChange,
  placeholder,
  className,
  triggerClassName,
  ariaLabel,
}: MultiSelectProps<T>) {
  const selectedSet = React.useMemo(() => new Set(value), [value]);

  const toggle = (v: T) => {
    const next = selectedSet.has(v)
      ? value.filter((x) => x !== v)
      : [...value, v];
    onChange(next);
  };

  const clear = () => onChange([]);

  const label =
    value.length === 0
      ? placeholder
      : `${placeholder} (${value.length})`;

  return (
    <div className={className}>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="outline"
            className={cn('justify-between gap-2', triggerClassName)}
            aria-label={ariaLabel ?? placeholder}
          >
            <span className={value.length === 0 ? 'text-muted-foreground' : ''}>
              {label}
            </span>
            <AppIcon name="chevronDown" className="opacity-50" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="start"
          className="w-[var(--radix-dropdown-menu-trigger-width)] min-w-[10rem]"
        >
          {options.map((opt) => (
            <DropdownMenuCheckboxItem
              key={opt.value}
              checked={selectedSet.has(opt.value)}
              onCheckedChange={() => toggle(opt.value)}
              onSelect={(e) => e.preventDefault()}
            >
              {opt.label}
            </DropdownMenuCheckboxItem>
          ))}
          {value.length > 0 && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onSelect={(e) => {
                  e.preventDefault();
                  clear();
                }}
                className="justify-center text-muted-foreground"
              >
                Xóa lọc
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
