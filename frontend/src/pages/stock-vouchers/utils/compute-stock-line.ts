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

// Splits `total` by `weights`; the last positive weight takes the rounding remainder (D10).
// Nothing is allocated when the weights sum to zero.
function allocate(total: number, weights: number[]): number[] {
  const allocations = weights.map(() => 0);
  const weightSum = sum(weights);
  if (weightSum <= 0) return allocations;

  let last = -1;
  weights.forEach((w, i) => {
    if (w > 0) last = i;
  });

  let allocated = 0;
  weights.forEach((w, i) => {
    if (i === last) return;
    allocations[i] = r0((total * w) / weightSum);
    allocated += allocations[i];
  });
  allocations[last] = total - allocated;
  return allocations;
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

  const orderDiscounts = allocate(header.orderDiscount, nets);
  const netsAfterOrderDiscount = nets.map((net, i) => net - orderDiscounts[i]);

  // Freight only raises the value of tracked goods received (VAT is never part of it, D6).
  const freights =
    header.direction === 'In'
      ? allocate(
          header.freight,
          netsAfterOrderDiscount.map((net, i) => (lines[i].trackInventory ? net : 0)),
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
