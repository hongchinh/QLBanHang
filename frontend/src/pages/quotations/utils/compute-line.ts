import type { PricingMode } from '@/features/products/types';
import { computePricingQuantity } from '@/lib/pricing-quantity';

export interface LineLike {
  pricingMode: PricingMode;
  length?: number;
  width?: number;
  thickness?: number;
  density?: number;
  sheetCount?: number;
  quantity: number;
  unitPrice: number;
  unitCost?: number;
}

export interface HeaderLike {
  taxRate: number;
  discount: number;
  freight: number;
  advancePayment: number;
}

export function round2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export function round0(value: number): number {
  return Math.round(value + Number.EPSILON);
}

export function computeLineQuantity(line: LineLike): number {
  return computePricingQuantity(line);
}

export function computeLineTotal(line: LineLike): number {
  return round0(computeLineQuantity(line) * line.unitPrice);
}

export function computeLineCost(line: LineLike): number | undefined {
  return line.unitCost != null ? round0(computeLineQuantity(line) * line.unitCost) : undefined;
}

export interface Totals {
  subtotal: number;
  totalCost: number;
  taxAmount: number;
  total: number;
  grossProfit: number;
  remainingBalance: number;
}

export function computeTotals(lines: LineLike[], header: HeaderLike): Totals {
  const subtotal = lines.reduce((sum, l) => sum + computeLineTotal(l), 0);
  const totalCost = lines.reduce((sum, l) => sum + (computeLineCost(l) ?? 0), 0);
  const taxAmount = round0((subtotal * header.taxRate) / 100);
  const total = subtotal - header.discount + header.freight + taxAmount;
  const grossProfit = subtotal - totalCost - header.discount;
  const remainingBalance = total - header.advancePayment;
  return { subtotal, totalCost, taxAmount, total, grossProfit, remainingBalance };
}
