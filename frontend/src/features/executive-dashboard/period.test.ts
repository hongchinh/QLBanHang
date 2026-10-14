import { describe, expect, it } from 'vitest';
import { bucketLabel, defaultGranularity, isDayGranularityAllowed, periodError, previousPeriod, rangeDays } from './period';

// The rules and messages of ExecutiveQueryValidator, which refuses such a period with 400.
describe('periodError', () => {
  it('accepts an ordered period of valid dates', () => {
    expect(periodError('2026-10-01', '2026-10-14')).toBeNull();
    expect(periodError('2026-10-14', '2026-10-14')).toBeNull();
    expect(periodError('2024-02-29', '2024-03-01')).toBeNull();
    expect(periodError('2000-01-01', '2000-01-31')).toBeNull();
    expect(periodError('2100-12-01', '2100-12-31')).toBeNull();
  });

  it('refuses "to" before "from"', () => {
    expect(periodError('2026-10-20', '2026-10-01')).toBe('Đến ngày phải sau hoặc bằng từ ngày.');
    expect(periodError('2026-10-02', '2026-10-01')).toBe('Đến ngày phải sau hoặc bằng từ ngày.');
  });

  it('accepts 1,100 days and refuses one more', () => {
    expect(rangeDays('2023-10-11', '2026-10-14')).toBe(1100);
    expect(periodError('2023-10-11', '2026-10-14')).toBeNull();
    expect(periodError('2023-10-10', '2026-10-14')).toBe('Khoảng thời gian tối đa là 3 năm.');
    expect(periodError('2023-01-01', '2026-10-14')).toBe('Khoảng thời gian tối đa là 3 năm.');
  });

  it.each(['1999-12-31', '2101-01-01', '2026-13-01', '2026-02-30', '2026-2-01', '20261-01-01', 'abc', ''])(
    'refuses "from" %j',
    (from) => {
      expect(periodError(from, '2026-10-14')).toBe('Từ ngày không hợp lệ.');
    },
  );

  it.each(['2101-01-01', '2026-10-32', '2026-10-14T00:00:00', ''])('refuses "to" %j', (to) => {
    expect(periodError('2026-10-01', to)).toBe('Đến ngày không hợp lệ.');
  });
});

describe('rangeDays', () => {
  it('counts both ends', () => {
    expect(rangeDays('2026-10-01', '2026-10-13')).toBe(13);
    expect(rangeDays('2026-10-13', '2026-10-13')).toBe(1);
    expect(rangeDays('2026-10-01', '2026-10-31')).toBe(31);
  });

  it('crosses months, leap days and years', () => {
    expect(rangeDays('2026-02-27', '2026-03-01')).toBe(3);
    expect(rangeDays('2024-02-28', '2024-03-01')).toBe(3);
    expect(rangeDays('2025-12-31', '2026-01-01')).toBe(2);
    expect(rangeDays('2026-01-01', '2026-12-31')).toBe(365);
    expect(rangeDays('2024-01-01', '2024-12-31')).toBe(366);
  });

  // A day with a clock change is 23 or 25 hours long where the browser observes one.
  it('is a whole number across the months where clocks change', () => {
    expect(rangeDays('2026-03-01', '2026-04-30')).toBe(61);
    expect(rangeDays('2026-10-01', '2026-11-30')).toBe(61);
  });
});

describe('previousPeriod', () => {
  it('has the same length and ends the day before "from"', () => {
    expect(previousPeriod('2026-10-01', '2026-10-13')).toEqual({ from: '2026-09-18', to: '2026-09-30' });
  });

  it('of one day is the day before', () => {
    expect(previousPeriod('2026-10-13', '2026-10-13')).toEqual({ from: '2026-10-12', to: '2026-10-12' });
    expect(previousPeriod('2026-01-01', '2026-01-01')).toEqual({ from: '2025-12-31', to: '2025-12-31' });
  });

  it('crosses months and years', () => {
    expect(previousPeriod('2026-01-01', '2026-01-31')).toEqual({ from: '2025-12-01', to: '2025-12-31' });
    // 31 days back from the end of February.
    expect(previousPeriod('2026-03-01', '2026-03-31')).toEqual({ from: '2026-01-29', to: '2026-02-28' });
    expect(previousPeriod('2024-03-01', '2024-03-31')).toEqual({ from: '2024-01-30', to: '2024-02-29' });
    // 286 days: day 80 of 2025 … day 365.
    expect(previousPeriod('2026-01-01', '2026-10-13')).toEqual({ from: '2025-03-21', to: '2025-12-31' });
  });
});

describe('defaultGranularity', () => {
  it('is Day up to 31 days', () => {
    expect(defaultGranularity('2026-10-01', '2026-10-31')).toBe('Day');
    expect(defaultGranularity('2026-10-13', '2026-10-13')).toBe('Day');
  });

  it('is Week from 32 to 92 days', () => {
    expect(defaultGranularity('2026-10-01', '2026-11-01')).toBe('Week');
    expect(defaultGranularity('2026-08-01', '2026-10-13')).toBe('Week');
    // 1 August … 31 October is 92 days.
    expect(defaultGranularity('2026-08-01', '2026-10-31')).toBe('Week');
  });

  it('is Month above 92 days', () => {
    expect(defaultGranularity('2026-08-01', '2026-11-01')).toBe('Month');
    expect(defaultGranularity('2026-01-01', '2026-10-13')).toBe('Month');
  });
});

describe('isDayGranularityAllowed', () => {
  it('allows up to 366 days, as the server does', () => {
    expect(isDayGranularityAllowed('2026-01-01', '2026-10-13')).toBe(true);
    expect(isDayGranularityAllowed('2024-01-01', '2024-12-31')).toBe(true);
    // 366 days ending today.
    expect(isDayGranularityAllowed('2025-10-13', '2026-10-13')).toBe(true);
  });

  it('refuses more', () => {
    expect(isDayGranularityAllowed('2025-10-12', '2026-10-13')).toBe(false);
    expect(isDayGranularityAllowed('2025-01-01', '2026-10-13')).toBe(false);
  });
});

describe('bucketLabel', () => {
  it('prints day and month for a day or a week bucket', () => {
    expect(bucketLabel('2026-10-05', 'Day')).toBe('05/10');
    expect(bucketLabel('2026-10-05', 'Week')).toBe('05/10');
    expect(bucketLabel('2026-01-31', 'Day')).toBe('31/01');
  });

  it('prints month and year for a month bucket', () => {
    expect(bucketLabel('2026-10-01', 'Month')).toBe('10/2026');
    expect(bucketLabel('2025-01-01', 'Month')).toBe('01/2025');
  });

  // The first week or month bucket can start before the period: the date is printed as it is.
  it('does not move the date of a bucket', () => {
    expect(bucketLabel('2026-09-28', 'Week')).toBe('28/09');
    expect(bucketLabel('2026-10-01T00:00:00', 'Day')).toBe('01/10');
  });
});
