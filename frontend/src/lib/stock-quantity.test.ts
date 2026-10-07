import { describe, expect, it } from 'vitest';
import { formatStockQuantity, parseQuantityInput } from './stock-quantity';

describe('parseQuantityInput', () => {
  it.each([
    ['10', 10],
    ['2.5', 2.5],
    [' 0.003659 ', 0.003659],
    ['-3', -3],
  ])('parses %s', (text, expected) => {
    expect(parseQuantityInput(text)).toBe(expected);
  });

  it.each(['', '   ', 'abc', '1,5', '1.500.000', '1e3'])('rejects %j', (text) => {
    expect(parseQuantityInput(text)).toBeUndefined();
  });
});

describe('formatStockQuantity', () => {
  it('keeps up to 6 decimals', () => {
    expect(formatStockQuantity(0.003659)).toBe('0.003659');
    expect(formatStockQuantity(undefined)).toBe('');
  });
});
