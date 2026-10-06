import type { PricingMode } from '@/features/products/types';

export interface PricingQuantityInput {
  pricingMode: PricingMode;
  sheetCount?: number;
  length?: number;
  width?: number;
  thickness?: number;
  quantity: number;
}

// Same formulas as backend PricingQuantity. Dimensions in mm; missing dimensions count as 0.
export function computePricingQuantity(input: PricingQuantityInput): number {
  const L = input.length ?? 0;
  const W = input.width ?? 0;
  const T = input.thickness ?? 0;
  const sheets = input.sheetCount ?? 0;
  switch (input.pricingMode) {
    case 'PerLinearMeter':
      return (L * sheets) / 1000;
    case 'PerSquareMeter':
      return (L * W * sheets) / 1_000_000;
    case 'PerCubicMeter':
      return (L * W * T * sheets) / 1_000_000_000;
    case 'PerUnit':
    default:
      return input.quantity;
  }
}
