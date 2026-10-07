import type { PricingMode } from '@/features/products/types';

// Stock quantities are stored with 6 decimals (numeric(18,6)); 0.003659 m³ must not show as 0.00.
// Same comma-group / dot-decimal style as the quotation quantity column.
const stockQuantityFmt = new Intl.NumberFormat('en-US', { maximumFractionDigits: 6 });

export function formatStockQuantity(value: number | undefined | null): string {
  if (value == null || !Number.isFinite(value)) return '';
  return stockQuantityFmt.format(value);
}

// Quantity input: dot decimal, no thousands grouping ("1.5" → 1.5). Empty or unparsable → undefined.
export function parseQuantityInput(text: string): number | undefined {
  const trimmed = text.trim();
  if (trimmed === '' || !/^-?\d*(?:\.\d*)?$/.test(trimmed)) return undefined;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : undefined;
}

const METRE_UNITS: Partial<Record<PricingMode, string>> = {
  PerLinearMeter: 'm',
  PerSquareMeter: 'm²',
  PerCubicMeter: 'm³',
};

// Unit of the stock quantity (D24): metre-based products are counted in m, m² or m³.
export function stockUnitName(pricingMode: PricingMode, unitName?: string | null): string {
  return METRE_UNITS[pricingMode] ?? unitName ?? '';
}
