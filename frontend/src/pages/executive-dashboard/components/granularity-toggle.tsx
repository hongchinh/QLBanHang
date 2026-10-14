import type { SeriesGranularity } from '@/features/executive-dashboard/types';
import { SegmentedToggle } from './segmented-toggle';

interface GranularityToggleProps {
  value: SeriesGranularity;
  onChange(granularity: SeriesGranularity): void;
  // The server refuses a daily series of more than 366 days.
  dayDisabled?: boolean;
}

// Ngày / Tuần / Tháng.
export function GranularityToggle({ value, onChange, dayDisabled }: GranularityToggleProps) {
  return (
    <SegmentedToggle
      label="Chia theo"
      options={[
        { key: 'Day', label: 'Ngày', disabled: dayDisabled },
        { key: 'Week', label: 'Tuần' },
        { key: 'Month', label: 'Tháng' },
      ]}
      value={value}
      onChange={onChange}
    />
  );
}
