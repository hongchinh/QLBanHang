import { describe, expect, it } from 'vitest';
import { formatPct, formatSignedCompact, formatVndCompact } from './format';

describe('formatVndCompact', () => {
  it.each([
    [2_840_000_000, '2,84 tỷ'],
    [1_260_000_000, '1,26 tỷ'],
    [3_100_000_000, '3,10 tỷ'],
    [412_000_000, '412 tr'],
    [45_500_000, '45,5 tr'],
    [999_960_000, '1,00 tỷ'],
    [950_000, '950.000'],
    [0, '0'],
    [-186_000_000, '-186 tr'],
  ])('%d → %s', (value, expected) => {
    expect(formatVndCompact(value)).toBe(expected);
  });

  it('prints the full number below one million and millions from one million', () => {
    expect(formatVndCompact(999_999)).toBe('999.999');
    expect(formatVndCompact(1_000_000)).toBe('1 tr');
    expect(formatVndCompact(-999_999)).toBe('-999.999');
    expect(formatVndCompact(-1_000_000)).toBe('-1 tr');
  });

  it('rounds millions to one decimal and drops a zero decimal', () => {
    expect(formatVndCompact(1_049_999)).toBe('1 tr');
    expect(formatVndCompact(1_050_000)).toBe('1,1 tr');
    expect(formatVndCompact(45_549_999.6)).toBe('45,5 tr');
    expect(formatVndCompact(412_300_000)).toBe('412,3 tr');
  });

  it('chooses the unit after rounding, so 1.000 tr is never printed', () => {
    expect(formatVndCompact(999_949_999)).toBe('999,9 tr');
    expect(formatVndCompact(999_950_000)).toBe('1,00 tỷ');
    expect(formatVndCompact(-999_960_000)).toBe('-1,00 tỷ');
    expect(formatVndCompact(1_000_000_000)).toBe('1,00 tỷ');
  });

  it('prints billions with exactly two decimals and vi-VN grouping', () => {
    expect(formatVndCompact(12_340_000_000)).toBe('12,34 tỷ');
    expect(formatVndCompact(-2_840_000_000)).toBe('-2,84 tỷ');
    expect(formatVndCompact(1_234_500_000_000)).toBe('1.234,50 tỷ');
  });
});

describe('formatSignedCompact', () => {
  it.each([
    [186_000_000, '+186 tr'],
    [-700_000, '-700.000'],
    [0, '0'],
    [700_000, '+700.000'],
    [2_840_000_000, '+2,84 tỷ'],
    [-186_000_000, '-186 tr'],
    [-2_840_000_000, '-2,84 tỷ'],
  ])('%d → %s', (value, expected) => {
    expect(formatSignedCompact(value)).toBe(expected);
  });

  it('prints the ASCII hyphen-minus, not the minus sign U+2212', () => {
    for (const text of [formatSignedCompact(-700_000), formatSignedCompact(-186_000_000), formatVndCompact(-2_840_000_000)]) {
      expect(text.charCodeAt(0)).toBe(0x2d);
    }
  });
});

describe('formatPct', () => {
  it.each([
    [14.5, '14,5%'],
    [100, '100%'],
    [33.33, '33,3%'],
    [0, '0%'],
    [-3.2, '-3,2%'],
    [1234.5, '1.234,5%'],
  ])('%d → %s', (value, expected) => {
    expect(formatPct(value)).toBe(expected);
  });

  it('prints "—" without a value', () => {
    expect(formatPct(null)).toBe('—');
    expect(formatPct(undefined)).toBe('—');
  });

  it('takes the number of decimals', () => {
    expect(formatPct(33.333, 2)).toBe('33,33%');
    expect(formatPct(14.56, 0)).toBe('15%');
    expect(formatPct(14.5, 2)).toBe('14,5%');
  });
});
