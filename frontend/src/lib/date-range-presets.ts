import { formatDateYmd } from '@/features/dashboard/format';

export type RangePreset = 'today' | '7d' | '30d' | 'this-week' | 'this-month' | 'last-month' | 'this-year';

export interface PresetOption {
  key: RangePreset;
  label: string;
}

// The chip presets, ranges that end today: every chip list (the dashboards' RangePicker, the quotation date
// filter) calls it. The report period presets (whole months, quarters and years, for PeriodPresetSelect) live
// in lib/period-presets.ts; neither module copies the other's ranges, and "Năm nay" differs between them
// (here 1 January to today, there the whole year).
export function computePreset(preset: RangePreset, today: Date = new Date()): { from: string; to: string } {
  let start: Date;
  let end: Date = today;
  switch (preset) {
    case 'today':
      start = today;
      break;
    case '7d':
      start = new Date(today);
      start.setDate(start.getDate() - 6);
      break;
    case '30d':
      start = new Date(today);
      start.setDate(start.getDate() - 29);
      break;
    case 'this-week':
      // Weeks start on Monday: getDay() is 0 on Sunday, which is the seventh day.
      start = new Date(today);
      start.setDate(start.getDate() - ((today.getDay() + 6) % 7));
      break;
    case 'this-month':
      start = new Date(today.getFullYear(), today.getMonth(), 1);
      break;
    case 'last-month':
      start = new Date(today.getFullYear(), today.getMonth() - 1, 1);
      end = new Date(today.getFullYear(), today.getMonth(), 0);
      break;
    case 'this-year':
      start = new Date(today.getFullYear(), 0, 1);
      break;
  }
  return { from: formatDateYmd(start), to: formatDateYmd(end) };
}

// The first key of `presets` whose range is from … to; a screen passes only the presets it offers.
export function matchActivePreset(
  from: string,
  to: string,
  presets: readonly RangePreset[],
  today: Date = new Date(),
): RangePreset | null {
  for (const preset of presets) {
    const expected = computePreset(preset, today);
    if (expected.from === from && expected.to === to) return preset;
  }
  return null;
}
