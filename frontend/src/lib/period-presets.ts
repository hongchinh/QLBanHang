import { todayYmd } from '@/lib/vn-datetime';

export type PeriodPresetKey = 'thisMonth' | 'lastMonth' | 'thisQuarter' | 'lastQuarter' | 'thisYear' | 'lastYear';

export const PERIOD_PRESET_LABELS: Record<PeriodPresetKey, string> = {
  thisMonth: 'Tháng này',
  lastMonth: 'Tháng trước',
  thisQuarter: 'Quý này',
  lastQuarter: 'Quý trước',
  thisYear: 'Năm nay',
  lastYear: 'Năm trước',
};

const pad = (n: number) => String(n).padStart(2, '0');

// Day count of a month (month is 1-based); day 0 of the next month is its last day.
const daysInMonth = (year: number, month: number) => new Date(Date.UTC(year, month, 0)).getUTCDate();

// Months startMonth..endMonth of one year, from the first to the last day.
function months(year: number, startMonth: number, endMonth: number) {
  return {
    from: `${year}-${pad(startMonth)}-01`,
    to: `${year}-${pad(endMonth)}-${pad(daysInMonth(year, endMonth))}`,
  };
}

// Works on the yyyy-MM-dd parts of `today` (the VN date), never through Date parsing or toISOString.
export function periodPreset(key: PeriodPresetKey, today: string = todayYmd()): { from: string; to: string } {
  const [year, month] = today.split('-').map(Number);
  const quarterStart = Math.floor((month - 1) / 3) * 3 + 1;

  switch (key) {
    case 'thisMonth':
      return months(year, month, month);
    case 'lastMonth':
      return month === 1 ? months(year - 1, 12, 12) : months(year, month - 1, month - 1);
    case 'thisQuarter':
      return months(year, quarterStart, quarterStart + 2);
    case 'lastQuarter':
      return quarterStart === 1 ? months(year - 1, 10, 12) : months(year, quarterStart - 3, quarterStart - 1);
    case 'thisYear':
      return months(year, 1, 12);
    case 'lastYear':
      return months(year - 1, 1, 12);
  }
}
