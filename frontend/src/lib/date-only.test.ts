import { describe, expect, it } from 'vitest';
import { formatDateOnly } from './date-only';

describe('formatDateOnly', () => {
  it('formats yyyy-MM-dd as dd/MM/yyyy', () => {
    expect(formatDateOnly('2026-10-05')).toBe('05/10/2026');
  });

  it('ignores a time part', () => {
    expect(formatDateOnly('2026-10-05T00:00:00')).toBe('05/10/2026');
  });

  it('returns an empty string for a missing value', () => {
    expect(formatDateOnly(null)).toBe('');
    expect(formatDateOnly(undefined)).toBe('');
    expect(formatDateOnly('')).toBe('');
  });
});
