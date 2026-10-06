import { describe, expect, it } from 'vitest';
import type { PricingMode } from '@/features/products/types';
import { computeStockVoucher, type StockHeaderLike, type StockLineLike } from './compute-stock-line';

// Same cases and numbers as backend StockVoucherCalculatorTests.
function line(
  quantity: number,
  unitPrice: number,
  vatRate: number,
  options: {
    pricingMode?: PricingMode;
    sheetCount?: number;
    length?: number;
    width?: number;
    thickness?: number;
    discountRate?: number;
    discountAmount?: number;
    discountManual?: boolean;
    priceIncludesVat?: boolean;
    trackInventory?: boolean;
  } = {},
): StockLineLike {
  return {
    trackInventory: options.trackInventory ?? true,
    pricingMode: options.pricingMode ?? 'PerUnit',
    priceIncludesVat: options.priceIncludesVat ?? false,
    sheetCount: options.sheetCount,
    length: options.length,
    width: options.width,
    thickness: options.thickness,
    quantity,
    unitPrice,
    discountRate: options.discountRate ?? 0,
    discountAmount: options.discountAmount,
    discountManual: options.discountManual ?? false,
    vatRate,
  };
}

function header(
  direction: 'In' | 'Out',
  options: { freight?: number; orderDiscount?: number; netExcludesVat?: boolean } = {},
): StockHeaderLike {
  return {
    direction,
    freight: options.freight ?? 0,
    orderDiscount: options.orderDiscount ?? 0,
    netExcludesVat: options.netExcludesVat ?? false,
  };
}

describe('computeStockVoucher', () => {
  it('Out example allocates order discount before VAT and handles a VAT-inclusive price', () => {
    const result = computeStockVoucher(header('Out', { freight: 20_000, orderDiscount: 30_000 }), [
      line(10, 100_000, 8, { discountRate: 10 }),
      line(0, 50_000, 10, {
        pricingMode: 'PerSquareMeter',
        sheetCount: 2,
        length: 2000,
        width: 1000,
        priceIncludesVat: true,
      }),
    ]);

    expect(result.lines[0]).toEqual({
      quantity: 10,
      amount: 1_000_000,
      discountAmount: 100_000,
      orderDiscountAllocated: 24_545,
      vatAmount: 70_036,
      netAmount: 945_491,
      freightAllocated: 0,
    });
    expect(result.lines[1]).toEqual({
      quantity: 4,
      amount: 200_000,
      discountAmount: 0,
      orderDiscountAllocated: 5_455,
      vatAmount: 17_686,
      netAmount: 194_545,
      freightAllocated: 0,
    });
    expect(result.totals).toEqual({
      goodsAmount: 1_200_000,
      lineDiscountTotal: 100_000,
      discountTotal: 130_000,
      vatTotal: 87_722,
      total: 1_160_036,
    });
  });

  it('In example allocates freight to tracked lines only', () => {
    const result = computeStockVoucher(header('In', { freight: 200_000, orderDiscount: 100_000 }), [
      line(100, 50_000, 10),
      line(0, 1_500_000, 8, {
        pricingMode: 'PerCubicMeter',
        sheetCount: 10,
        length: 2000,
        width: 1000,
        thickness: 50,
        discountRate: 5,
      }),
      line(1, 300_000, 8, { trackInventory: false }),
    ]);

    expect(result.lines.map((l) => l.amount - l.discountAmount)).toEqual([5_000_000, 1_425_000, 300_000]);
    expect(result.lines.map((l) => l.orderDiscountAllocated)).toEqual([74_349, 21_190, 4_461]);
    expect(result.lines.map((l) => l.amount - l.discountAmount - l.orderDiscountAllocated)).toEqual([
      4_925_651, 1_403_810, 295_539,
    ]);
    expect(result.lines.map((l) => l.vatAmount)).toEqual([492_565, 112_305, 23_643]);
    expect(result.lines.map((l) => l.netAmount)).toEqual([5_418_216, 1_516_115, 319_182]);
    expect(result.lines.map((l) => l.freightAllocated)).toEqual([155_642, 44_358, 0]);
    expect(result.totals).toEqual({
      goodsAmount: 6_800_000,
      lineDiscountTotal: 75_000,
      discountTotal: 175_000,
      vatTotal: 628_513,
      total: 7_453_513,
    });
  });

  it('manual discount amount overrides the rate', () => {
    const [computed] = computeStockVoucher(header('Out'), [
      line(3, 33_333, 0, { discountRate: 50, discountAmount: 1_000, discountManual: true }),
    ]).lines;

    expect(computed.amount).toBe(99_999);
    expect(computed.discountAmount).toBe(1_000);
    expect(computed.netAmount).toBe(98_999);
  });

  it('netExcludesVat keeps VAT out of the net amount', () => {
    const [computed] = computeStockVoucher(header('Out', { netExcludesVat: true }), [
      line(1, 100_000, 10),
    ]).lines;

    expect(computed.vatAmount).toBe(10_000);
    expect(computed.netAmount).toBe(100_000);
  });

  it('VAT-inclusive flag is ignored on stock-in', () => {
    const [computed] = computeStockVoucher(header('In'), [
      line(1, 110_000, 10, { priceIncludesVat: true }),
    ]).lines;

    expect(computed.vatAmount).toBe(11_000);
    expect(computed.netAmount).toBe(121_000);
  });

  it('order discount remainder goes to the last line with a positive net', () => {
    const result = computeStockVoucher(header('Out', { orderDiscount: 10_000 }), [
      line(1, 100_000, 0),
      line(1, 50_000, 0),
      line(1, 0, 0),
    ]);

    expect(result.lines.map((l) => l.orderDiscountAllocated)).toEqual([6_667, 3_333, 0]);
  });

  it('quantity is rounded to six decimals before the amount', () => {
    const [computed] = computeStockVoucher(header('In'), [
      line(0, 10_000_000, 0, {
        pricingMode: 'PerCubicMeter',
        sheetCount: 1,
        length: 333,
        width: 333,
        thickness: 33,
      }),
    ]).lines;

    expect(computed.quantity).toBe(0.003659);
    expect(computed.amount).toBe(36_590);
  });

  it('freight without tracked lines is not allocated', () => {
    const result = computeStockVoucher(header('In', { freight: 50_000 }), [
      line(1, 100_000, 0, { trackInventory: false }),
      line(2, 30_000, 0, { trackInventory: false }),
    ]);

    expect(result.lines.map((l) => l.freightAllocated)).toEqual([0, 0]);
    expect(result.totals.total).toBe(210_000);
  });
});
