import type { SeriesGranularity } from '@/features/executive-dashboard/types';
import { cn } from '@/lib/utils';

interface GranularityToggleProps {
  value: SeriesGranularity;
  onChange(granularity: SeriesGranularity): void;
  // The server refuses a daily series of more than 366 days.
  dayDisabled?: boolean;
}

const OPTIONS: { key: SeriesGranularity; label: string }[] = [
  { key: 'Day', label: 'Ngày' },
  { key: 'Week', label: 'Tuần' },
  { key: 'Month', label: 'Tháng' },
];

// Ngày / Tuần / Tháng, with the look of the toggle of the quotation revenue chart.
export function GranularityToggle({ value, onChange, dayDisabled }: GranularityToggleProps) {
  return (
    <div role="group" aria-label="Chia theo" className="flex items-center justify-end gap-1">
      {OPTIONS.map((option) => (
        <button
          key={option.key}
          type="button"
          aria-pressed={value === option.key}
          disabled={option.key === 'Day' && dayDisabled}
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
