import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import {
  firstDayOfMonthYmd,
  fromDateTimeLocalValue,
  toDateTimeLocalValue,
  todayYmd,
} from './vn-datetime';

// D14: the browser runs in VN time. Pin it so the test does not depend on the machine.
const originalTz = process.env.TZ;
beforeAll(() => {
  process.env.TZ = 'Asia/Ho_Chi_Minh';
});
afterAll(() => {
  process.env.TZ = originalTz;
});

describe('vn-datetime', () => {
  it('converts an instant to a local datetime-local value', () => {
    expect(toDateTimeLocalValue('2026-10-31T22:00:00+00:00')).toBe('2026-11-01T05:00');
  });

  it('round-trip keeps the instant', () => {
    const iso = '2026-10-06T02:00:00.000Z';
    expect(fromDateTimeLocalValue(toDateTimeLocalValue(iso))).toBe(iso);
  });

  it('builds dates from local date parts, not UTC', () => {
    // 2026-10-01 06:00 VN = 2026-09-30 23:00 UTC; toISOString().slice(0, 10) would give 09-30.
    const now = new Date('2026-09-30T23:00:00.000Z');
    expect(firstDayOfMonthYmd(now)).toBe('2026-10-01');
    expect(todayYmd(now)).toBe('2026-10-01');
  });
});
