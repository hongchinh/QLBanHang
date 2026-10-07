import type { PricingMode } from '@/features/products/types';
import { computePricingQuantity } from '@/lib/pricing-quantity';
import { roundAwayFromZero } from '@/lib/round';

export interface StockLineLike {
  trackInventory: boolean;
  pricingMode: PricingMode;
  priceIncludesVat: boolean;
  sheetCount?: number;
  length?: number;
  width?: number;
  thickness?: number;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  discountAmount?: number;
  discountManual: boolean;
  vatRate: number;
}

export interface StockHeaderLike {
  direction: 'In' | 'Out';
  freight: number;
  orderDiscount: number;
  netExcludesVat: boolean;
}

export interface StockLineComputed {
  quantity: number;
  amount: number;
  discountAmount: number;
  orderDiscountAllocated: number;
  vatAmount: number;
  netAmount: number;
  freightAllocated: number;
}

export interface StockTotals {
  goodsAmount: number;
  lineDiscountTotal: number;
  discountTotal: number;
  vatTotal: number;
  total: number;
}

const r0 = (x: number) => roundAwayFromZero(x, 0);
const r6 = (x: number) => roundAwayFromZero(x, 6);
const sum = (values: number[]) => values.reduce((acc, v) => acc + v, 0);

const r2 = (x: number) => roundAwayFromZero(x, 2);
const EPSILON = 1e-9;

// Mirrors backend StockVoucherCalculator.Allocate. Nothing is allocated when the weights sum to zero.
// Primary split (D10): each positive weight gets its rounded share and the last positive weight takes
// the rounding remainder. When that remainder would go negative, or (capAtWeight) push a line above its
// own weight, the split falls back to largest remainder on whole units instead.
function allocate(total: number, weights: number[], capAtWeight: boolean): number[] {
  const weightSum = sum(weights);
  if (weightSum <= 0) return weights.map(() => 0);

  let last = -1;
  weights.forEach((w, i) => {
    if (w > 0) last = i;
  });

  const primary = weights.map((w, i) => (w > 0 && i !== last ? r0((total * w) / weightSum) : 0));
  primary[last] = total - sum(primary);
  const valid = primary.every((a, i) => a >= 0 && (!capAtWeight || a <= weights[i]));
  if (valid) return primary;

  const shares = weights.map((w) => (w > 0 ? (total * w) / weightSum : 0));
  const allocations = shares.map((share) => Math.floor(share + EPSILON));
  let left = total - sum(allocations);
  // Largest fractional part first; ties go to the later line.
  const order = weights
    .map((w, i) => ({ i, fraction: shares[i] - allocations[i], positive: w > 0 }))
    .filter((x) => x.positive)
    .sort((a, b) => b.fraction - a.fraction || b.i - a.i)
    .map((x) => x.i);
  while (left > EPSILON) {
    for (const i of order) {
      if (left <= EPSILON) break;
      const add = Math.min(1, left);
      allocations[i] += add;
      left -= add;
    }
  }
  return allocations.map(r2);
}

// Live preview of backend StockVoucherCalculator (D6, D9, D10); the backend stays authoritative.
// `lines` must already be in sort order.
export function computeStockVoucher(
  header: StockHeaderLike,
  lines: StockLineLike[],
): { lines: StockLineComputed[]; totals: StockTotals } {
  const quantities = lines.map((line) => r6(computePricingQuantity(line)));
  const amounts = lines.map((line, i) => r0(quantities[i] * line.unitPrice));
  const discounts = lines.map((line, i) =>
    line.discountManual ? r0(line.discountAmount ?? 0) : r0((amounts[i] * line.discountRate) / 100),
  );
  const nets = amounts.map((amount, i) => amount - discounts[i]);

  // The order discount never takes a line below zero (capped at its Net).
  const orderDiscounts = allocate(header.orderDiscount, nets, true);
  const netsAfterOrderDiscount = nets.map((net, i) => net - orderDiscounts[i]);

  // Freight only raises the value of tracked goods received (VAT is never part of it, D6).
  const freights =
    header.direction === 'In'
      ? allocate(
          header.freight,
          netsAfterOrderDiscount.map((net, i) => (lines[i].trackInventory ? net : 0)),
          false,
        )
      : lines.map(() => 0);

  const computed = lines.map((line, i): StockLineComputed => {
    const net = netsAfterOrderDiscount[i];
    const vatInclusive = header.direction === 'Out' && line.priceIncludesVat;
    const vatAmount = vatInclusive
      ? r0(net - net / (1 + line.vatRate / 100))
      : r0((net * line.vatRate) / 100);
    return {
      quantity: quantities[i],
      amount: amounts[i],
      discountAmount: discounts[i],
      orderDiscountAllocated: orderDiscounts[i],
      vatAmount,
      netAmount: vatInclusive || header.netExcludesVat ? net : net + vatAmount,
      freightAllocated: freights[i],
    };
  });

  const lineDiscountTotal = sum(computed.map((c) => c.discountAmount));
  return {
    lines: computed,
    totals: {
      goodsAmount: sum(computed.map((c) => c.amount)),
      lineDiscountTotal,
      discountTotal: lineDiscountTotal + header.orderDiscount,
      vatTotal: sum(computed.map((c) => c.vatAmount)),
      total: sum(computed.map((c) => c.netAmount)) + header.freight,
    },
  };
}
