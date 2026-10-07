import { describe, expect, it } from 'vitest';
import { toProductSuggestion } from './to-product-suggestion';
import type { ProductListItem } from './types';

describe('toProductSuggestion', () => {
  it('toProductSuggestion maps the inventory fields', () => {
    const item: ProductListItem = {
      id: 'p1',
      code: 'SP01',
      name: 'Tôn lạnh',
      productGroupName: 'Tôn',
      unitName: 'Tấm',
      specification: '0.45mm',
      defaultPrice: 120_000,
      costPrice: 100_000,
      status: 'Active',
      pricingMode: 'PerSquareMeter',
      trackInventory: false,
      defaultTaxRate: 8,
      length: 2,
      width: 1.2,
      thickness: 0.45,
      purchaseDiscountRate: 5,
      salesDiscountRate: 3,
      priceIncludesVat: true,
    };

    expect(toProductSuggestion(item)).toEqual({
      id: 'p1',
      code: 'SP01',
      name: 'Tôn lạnh',
      specification: '0.45mm',
      unitName: 'Tấm',
      pricingMode: 'PerSquareMeter',
      defaultPrice: 120_000,
      costPrice: 100_000,
      defaultTaxRate: 8,
      length: 2,
      width: 1.2,
      thickness: 0.45,
      trackInventory: false,
      purchaseDiscountRate: 5,
      salesDiscountRate: 3,
      priceIncludesVat: true,
    });
  });
});
