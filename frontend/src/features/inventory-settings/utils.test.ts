import { describe, expect, it } from 'vitest';
import { formatDocumentNumber, listPeriodStarts } from './utils';

describe('formatDocumentNumber', () => {
  it('formats the default pattern', () => {
    expect(formatDocumentNumber('{KH}{STT}', 'PN', 5, 12, new Date(2026, 9, 6))).toBe('PN00012');
  });

  it('formats year and month tokens', () => {
    expect(formatDocumentNumber('{KH}{NAM}{THANG}-{STT}', 'PX', 4, 7, new Date(2026, 2, 1))).toBe(
      'PX202603-0007',
    );
  });

  it('does not truncate a counter longer than the length', () => {
    expect(formatDocumentNumber('{KH}{STT}', 'PN', 3, 12345, new Date(2026, 9, 6))).toBe('PN12345');
  });
});

describe('listPeriodStarts', () => {
  it('listPeriodStarts returns month/quarter/year starts in descending order', () => {
    const today = new Date(2026, 9, 7);
    expect(listPeriodStarts('Month', today, 3)).toEqual(['2026-10-01', '2026-09-01', '2026-08-01']);
    expect(listPeriodStarts('Month', new Date(2026, 1, 15), 3)).toEqual([
      '2026-02-01',
      '2026-01-01',
      '2025-12-01',
    ]);
    expect(listPeriodStarts('Quarter', today, 4)).toEqual([
      '2026-10-01',
      '2026-07-01',
      '2026-04-01',
      '2026-01-01',
    ]);
    expect(listPeriodStarts('Quarter', new Date(2026, 1, 15), 2)).toEqual(['2026-01-01', '2025-10-01']);
    expect(listPeriodStarts('Year', today, 2)).toEqual(['2026-01-01', '2025-01-01']);
  });
});
