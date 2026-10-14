import { formatDateYmd } from '@/features/dashboard/format';
import type { SeriesGranularity } from './types';

// Date rules of the dashboard period. Dates are 'yyyy-MM-dd' strings; they are read by hand into a local
// Date (Date parsing reads that form as UTC and shifts the day west of Greenwich).

const DAY_MS = 86_400_000;
// The server refuses a daily series longer than this.
const MAX_DAILY_DAYS = 366;
// The limits of ExecutiveQueryValidator (ReportDates.Min/Max, MaxDays).
const MIN_DATE = '2000-01-01';
const MAX_DATE = '2100-12-31';
const MAX_DAYS = 1100;

function parseYmd(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

// A real 'yyyy-MM-dd' date within the dates the server accepts (strings of that form compare as dates).
function isAcceptedDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value < MIN_DATE || value > MAX_DATE) return false;
  // 2026-02-30 would roll over into March.
  return formatDateYmd(parseYmd(value)) === value;
}

// Why the server would refuse the period (its message), or null: the dates must exist, lie between 2000 and
// 2100, be in order and span at most 1,100 days.
export function periodError(from: string, to: string): string | null {
  if (!isAcceptedDate(from)) return 'Từ ngày không hợp lệ.';
  if (!isAcceptedDate(to)) return 'Đến ngày không hợp lệ.';
  if (to < from) return 'Đến ngày phải sau hoặc bằng từ ngày.';
  if (rangeDays(from, to) > MAX_DAYS) return 'Khoảng thời gian tối đa là 3 năm.';
  return null;
}

function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

// Number of days from … to, both included.
export function rangeDays(from: string, to: string): number {
  // Rounded: a day with a clock change is 23 or 25 hours long.
  return Math.round((parseYmd(to).getTime() - parseYmd(from).getTime()) / DAY_MS) + 1;
}

// The period of the same length that ends the day before `from` (what the server compares with).
export function previousPeriod(from: string, to: string): { from: string; to: string } {
  const start = parseYmd(from);
  return {
    from: formatDateYmd(addDays(start, -rangeDays(from, to))),
    to: formatDateYmd(addDays(start, -1)),
  };
}

// Up to 31 days by day, up to 92 by week, longer by month.
export function defaultGranularity(from: string, to: string): SeriesGranularity {
  const days = rangeDays(from, to);
  if (days <= 31) return 'Day';
  if (days <= 92) return 'Week';
  return 'Month';
}

export function isDayGranularityAllowed(from: string, to: string): boolean {
  return rangeDays(from, to) <= MAX_DAILY_DAYS;
}

// X-axis label of a series bucket: dd/MM for a day or a week (its Monday), MM/yyyy for a month.
// The bucket date is printed as it is: the first week or month bucket can start before the period.
export function bucketLabel(date: string, granularity: SeriesGranularity): string {
  const [year, month, day] = date.slice(0, 10).split('-');
  return granularity === 'Month' ? `${month}/${year}` : `${day}/${month}`;
}

const GRANULARITY_UNIT: Record<SeriesGranularity, string> = { Day: 'ngày', Week: 'tuần', Month: 'tháng' };

// "theo ngày, 01/10 – 13/10": the kind of bucket and the first and last bucket, for the text of a chart.
export function seriesSpan(dates: string[], granularity: SeriesGranularity): string {
  const unit = `theo ${GRANULARITY_UNIT[granularity]}`;
  if (dates.length === 0) return unit;
  const first = bucketLabel(dates[0], granularity);
  const last = bucketLabel(dates[dates.length - 1], granularity);
  return `${unit}, ${first === last ? first : `${first} – ${last}`}`;
}
