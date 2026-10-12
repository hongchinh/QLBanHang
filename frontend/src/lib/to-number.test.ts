import { describe, expect, it } from 'vitest';
import { toNumberOrZero, toOptionalNumber } from './to-number';

describe('toNumberOrZero', () => {
  it('converts numeric input and falls back to 0', () => {
    expect(toNumberOrZero('12.5')).toBe(12.5);
    expect(toNumberOrZero('x')).toBe(0);
    expect(toNumberOrZero(undefined)).toBe(0);
  });
});

describe('toOptionalNumber', () => {
  it('returns undefined for empty or invalid input', () => {
    expect(toOptionalNumber('')).toBeUndefined();
    expect(toOptionalNumber(null)).toBeUndefined();
    expect(toOptionalNumber(undefined)).toBeUndefined();
    expect(toOptionalNumber('x')).toBeUndefined();
  });

  it('passes a number through', () => {
    expect(toOptionalNumber(3)).toBe(3);
  });
});
