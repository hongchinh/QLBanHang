import type { CostingPeriod } from './types';

// TS port of the backend DocumentNumberFormatter.Format (D13), used for the live preview.
// {STT} is zero-padded to `length` and never truncated. Replacer functions keep `$` in the prefix literal.
export function formatDocumentNumber(
  pattern: string,
  prefix: string,
  length: number,
  counter: number,
  date: Date,
): string {
  const year = String(date.getFullYear()).padStart(4, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const stt = String(counter).padStart(length, '0');
  return pattern
    .replaceAll('{KH}', () => prefix)
    .replaceAll('{NAM}', () => year)
    .replaceAll('{THANG}', () => month)
    .replaceAll('{STT}', () => stt);
}

const MONTHS_PER_PERIOD: Record<CostingPeriod, number> = { Month: 1, Quarter: 3, Year: 12 };

// The `count` most recent costing-period starts (`yyyy-MM-dd`, local date), newest first,
// beginning with the period that contains `today`.
export function listPeriodStarts(period: CostingPeriod, today: Date, count: number): string[] {
  const step = MONTHS_PER_PERIOD[period];
  const firstMonth = Math.floor(today.getMonth() / step) * step;
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(today.getFullYear(), firstMonth - i * step, 1);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
  });
}
