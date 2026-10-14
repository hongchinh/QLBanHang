import { cn } from '@/lib/utils';

export interface SegmentedOption<T extends string> {
  key: T;
  label: string;
  disabled?: boolean;
}

interface SegmentedToggleProps<T extends string> {
  // Accessible name of the group.
  label: string;
  options: SegmentedOption<T>[];
  value: T;
  onChange(value: T): void;
}

// A row of small buttons of which one is chosen, with the look of the toggle of the quotation revenue chart.
export function SegmentedToggle<T extends string>({ label, options, value, onChange }: SegmentedToggleProps<T>) {
  return (
    <div role="group" aria-label={label} className="flex items-center justify-end gap-1">
      {options.map((option) => (
        <button
          key={option.key}
          type="button"
          aria-pressed={value === option.key}
          disabled={option.disabled}
          onClick={() => onChange(option.key)}
          className={cn(
            'rounded-md border px-2 py-1 text-xs font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50',
            value === option.key
              ? 'border-foreground bg-foreground text-background'
              : 'border-border bg-card text-muted-foreground hover:bg-accent',
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
