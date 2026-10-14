import type { PresetOption } from '@/lib/date-range-presets';

// The period chips of the executive dashboard (the range picker adds "Tuỳ chỉnh").
export const EXECUTIVE_PRESETS: PresetOption[] = [
  { key: 'today', label: 'Hôm nay' },
  { key: 'this-week', label: 'Tuần này' },
  { key: 'this-month', label: 'Tháng này' },
  { key: 'this-year', label: 'Năm nay' },
];
