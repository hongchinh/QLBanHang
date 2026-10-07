import type { Product, ProductListItem, ProductSuggestion } from './types';

// One selection rule for every product picker path (typeahead, catalog list, catalog detail).
export function toProductSuggestion(p: Product | ProductListItem): ProductSuggestion {
  return {
    id: p.id,
    code: p.code,
    name: p.name,
    specification: p.specification,
    unitName: p.unitName,
    pricingMode: p.pricingMode,
    defaultPrice: p.defaultPrice,
    costPrice: p.costPrice,
    defaultTaxRate: p.defaultTaxRate,
    length: p.length,
    width: p.width,
    thickness: p.thickness,
    trackInventory: p.trackInventory,
    purchaseDiscountRate: p.purchaseDiscountRate,
    salesDiscountRate: p.salesDiscountRate,
    priceIncludesVat: p.priceIncludesVat,
  };
}
