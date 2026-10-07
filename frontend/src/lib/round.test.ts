import { describe, expect, it } from 'vitest';
import { roundAwayFromZero } from './round';

describe('roundAwayFromZero', () => {
  it('rounds a negative midpoint away from zero', () => {
    expect(roundAwayFromZero(-2.5)).toBe(-3);
  });

  it('rounds a positive midpoint away from zero', () => {
    expect(roundAwayFromZero(2.5)).toBe(3);
  });

  it('rounds 1.005 at 2 digits to 1.01', () => {
    expect(roundAwayFromZero(1.005, 2)).toBe(1.01);
  });

  it('rounds an exact .5 VAT amount up (Net 105 x 10% = 10.5)', () => {
    expect(roundAwayFromZero((105 * 10) / 100)).toBe(11);
  });
});
