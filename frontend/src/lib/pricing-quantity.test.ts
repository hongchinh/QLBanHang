import { describe, expect, it } from 'vitest';
import { computePricingQuantity } from './pricing-quantity';

describe('computePricingQuantity', () => {
  it('PerUnit returns quantity', () => {
    expect(computePricingQuantity({ pricingMode: 'PerUnit', quantity: 7 })).toBe(7);
  });

  it('PerLinearMeter is sheets x length / 1000', () => {
    expect(
      computePricingQuantity({ pricingMode: 'PerLinearMeter', sheetCount: 4, length: 2500, quantity: 0 }),
    ).toBe(10);
  });

  it('PerSquareMeter is sheets x length x width / 1e6', () => {
    expect(
      computePricingQuantity({
        pricingMode: 'PerSquareMeter',
        sheetCount: 2,
        length: 2000,
        width: 1000,
        quantity: 0,
      }),
    ).toBe(4);
  });

  it('PerCubicMeter is sheets x dimensions / 1e9', () => {
    expect(
      computePricingQuantity({
        pricingMode: 'PerCubicMeter',
        sheetCount: 1,
        length: 1000,
        width: 1000,
        thickness: 500,
        quantity: 0,
      }),
    ).toBe(0.5);
  });

  it('missing dimensions yield zero', () => {
    expect(
      computePricingQuantity({ pricingMode: 'PerSquareMeter', sheetCount: 2, length: 2000, quantity: 5 }),
    ).toBe(0);
  });
});
