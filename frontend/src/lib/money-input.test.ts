import { describe, expect, it } from 'vitest';
import { formatMoneyForDisplay, parseMoneyInput } from './money-input';

describe('parseMoneyInput', () => {
  it('parses vi-VN thousand-separated integers', () => {
    expect(parseMoneyInput('3.000.000')).toBe(3000000);
  });

  it('parses plain integer strings', () => {
    expect(parseMoneyInput('3000000')).toBe(3000000);
  });

  it('parses vi-VN decimal comma', () => {
    expect(parseMoneyInput('3,5')).toBe(3.5);
  });

  it('parses mixed thousand and decimal vi-VN', () => {
    expect(parseMoneyInput('1.234,56')).toBe(1234.56);
  });

  it('returns undefined for empty string', () => {
    expect(parseMoneyInput('')).toBeUndefined();
  });

  it('returns undefined for whitespace', () => {
    expect(parseMoneyInput('   ')).toBeUndefined();
  });

  it('returns undefined for non-numeric text', () => {
    expect(parseMoneyInput('abc')).toBeUndefined();
  });

  it('parses negative numbers', () => {
    expect(parseMoneyInput('-1500')).toBe(-1500);
  });
});

describe('formatMoneyForDisplay', () => {
  it('formats integers with vi-VN thousand separators', () => {
    expect(formatMoneyForDisplay(3000000)).toBe('3.000.000');
  });

  it('formats zero as "0"', () => {
    expect(formatMoneyForDisplay(0)).toBe('0');
  });

  it('returns empty string for undefined', () => {
    expect(formatMoneyForDisplay(undefined)).toBe('');
  });

  it('returns empty string for null', () => {
    expect(formatMoneyForDisplay(null)).toBe('');
  });

  it('returns empty string for empty string', () => {
    expect(formatMoneyForDisplay('')).toBe('');
  });

  it('accepts numeric strings', () => {
    expect(formatMoneyForDisplay('1500')).toBe('1.500');
  });

  it('returns empty string for NaN', () => {
    expect(formatMoneyForDisplay(NaN)).toBe('');
  });
});
