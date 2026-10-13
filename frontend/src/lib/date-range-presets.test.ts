import { afterEach, describe, expect, it, vi } from 'vitest';
import { computePreset, matchActivePreset } from './date-range-presets';
import type { RangePreset } from './date-range-presets';

const TUESDAY = new Date(2026, 9, 13);
const MONDAY = new Date(2026, 9, 12);
const SUNDAY = new Date(2026, 9, 18);

describe('computePreset', () => {
  it.each<[RangePreset, string, string]>([
    ['today', '2026-10-13', '2026-10-13'],
    ['this-week', '2026-10-12', '2026-10-13'],
    ['this-month', '2026-10-01', '2026-10-13'],
    ['this-year', '2026-01-01', '2026-10-13'],
    ['last-month', '2026-09-01', '2026-09-30'],
    ['7d', '2026-10-07', '2026-10-13'],
    ['30d', '2026-09-14', '2026-10-13'],
  ])('%s on Tuesday 2026-10-13 is %s … %s', (preset, from, to) => {
    expect(computePreset(preset, TUESDAY)).toEqual({ from, to });
  });

  it('this-week starts on Monday: a Sunday closes the week that began six days before', () => {
    expect(computePreset('this-week', SUNDAY)).toEqual({ from: '2026-10-12', to: '2026-10-18' });
  });

  it('this-week on a Monday is that one day', () => {
    expect(computePreset('this-week', MONDAY)).toEqual({ from: '2026-10-12', to: '2026-10-12' });
  });

  it('this-week may start in the month and the year before', () => {
    // Friday 1 January 2027.
    expect(computePreset('this-week', new Date(2027, 0, 1))).toEqual({ from: '2026-12-28', to: '2027-01-01' });
  });

  it('this-year on 1 January is that one day', () => {
    expect(computePreset('this-year', new Date(2026, 0, 1))).toEqual({ from: '2026-01-01', to: '2026-01-01' });
  });

  it('last-month in January is December of the year before', () => {
    expect(computePreset('last-month', new Date(2026, 0, 15))).toEqual({ from: '2025-12-01', to: '2025-12-31' });
  });

  it('does not change the date it is given', () => {
    const today = new Date(2026, 9, 13, 15, 30);
    const before = today.getTime();
    for (const preset of ['today', '7d', '30d', 'this-week', 'this-month', 'last-month', 'this-year'] as const) {
      computePreset(preset, today);
    }
    expect(today.getTime()).toBe(before);
  });

  describe('without a date', () => {
    afterEach(() => vi.useRealTimers());

    it('uses the current day', () => {
      vi.useFakeTimers({ toFake: ['Date'] });
      vi.setSystemTime(new Date(2026, 9, 13, 23, 30));
      expect(computePreset('today')).toEqual({ from: '2026-10-13', to: '2026-10-13' });
      expect(matchActivePreset('2026-10-12', '2026-10-13', ['this-week'])).toBe('this-week');
    });
  });
});

describe('matchActivePreset', () => {
  const EXECUTIVE: RangePreset[] = ['today', 'this-week', 'this-month', 'this-year'];
  const QUOTATION: RangePreset[] = ['7d', '30d', 'this-month', 'last-month'];

  it('returns the key whose range is from … to', () => {
    expect(matchActivePreset('2026-10-12', '2026-10-13', EXECUTIVE, TUESDAY)).toBe('this-week');
    expect(matchActivePreset('2026-01-01', '2026-10-13', EXECUTIVE, TUESDAY)).toBe('this-year');
    expect(matchActivePreset('2026-09-01', '2026-09-30', QUOTATION, TUESDAY)).toBe('last-month');
  });

  it('looks only at the keys it is given', () => {
    expect(matchActivePreset('2026-10-12', '2026-10-13', QUOTATION, TUESDAY)).toBeNull();
    expect(matchActivePreset('2026-10-13', '2026-10-13', QUOTATION, TUESDAY)).toBeNull();
    expect(matchActivePreset('2026-10-12', '2026-10-13', [], TUESDAY)).toBeNull();
  });

  it('the first key of the list wins when two ranges are equal', () => {
    // On a Monday "today" and "this-week" are the same range.
    expect(matchActivePreset('2026-10-12', '2026-10-12', EXECUTIVE, MONDAY)).toBe('today');
    expect(matchActivePreset('2026-10-12', '2026-10-12', ['this-week', 'today'], MONDAY)).toBe('this-week');
  });

  it('needs both ends to match', () => {
    expect(matchActivePreset('2026-10-12', '2026-10-12', EXECUTIVE, TUESDAY)).toBeNull();
    expect(matchActivePreset('2026-10-11', '2026-10-13', EXECUTIVE, TUESDAY)).toBeNull();
  });
});
