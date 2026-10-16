import type { PricingMode } from '@/features/products/types';

// Dimension columns of the line grids, in column order (also the cell id names).
export const DIMENSION_FIELDS = ['sheet-count', 'length', 'width', 'thickness'] as const;
export type DimensionField = (typeof DIMENSION_FIELDS)[number];

export function isDimensionField(field: string): field is DimensionField {
  return (DIMENSION_FIELDS as readonly string[]).includes(field);
}

// Pricing modes in rank order. The PricingMode union lists m² before md, so the rank comes only from here.
export const PRICING_MODES: readonly PricingMode[] = ['PerUnit', 'PerLinearMeter', 'PerSquareMeter', 'PerCubicMeter'];

export const PRICING_MODE_RANK = Object.fromEntries(
  PRICING_MODES.map((mode, rank) => [mode, rank]),
) as Record<PricingMode, number>;

// The lowest rank that uses each dimension: md uses Số tấm and Dài, m² adds Rộng, m³ adds Dày.
const MIN_RANK: Record<DimensionField, number> = { 'sheet-count': 1, length: 1, width: 2, thickness: 3 };

// A missing or unknown mode (an old draft, corrupt data) counts as PerUnit.
export function normalizePricingMode(mode: unknown): PricingMode {
  return (PRICING_MODES as readonly unknown[]).includes(mode) ? (mode as PricingMode) : 'PerUnit';
}

export function usesDimension(mode: unknown, field: DimensionField): boolean {
  return PRICING_MODE_RANK[normalizePricingMode(mode)] >= MIN_RANK[field];
}

export function highestPricingMode(lines: readonly { pricingMode?: unknown }[]): PricingMode {
  let rank = 0;
  for (const line of lines) rank = Math.max(rank, PRICING_MODE_RANK[normalizePricingMode(line.pricingMode)]);
  return PRICING_MODES[rank];
}

// The dimension columns a grid shows: those the highest mode uses, plus the forced ones, in column order.
export function shownDimensions(
  lines: readonly { pricingMode?: unknown }[],
  forced: Iterable<DimensionField> = [],
): DimensionField[] {
  const highest = highestPricingMode(lines);
  const extra = new Set(forced);
  return DIMENSION_FIELDS.filter((field) => usesDimension(highest, field) || extra.has(field));
}
