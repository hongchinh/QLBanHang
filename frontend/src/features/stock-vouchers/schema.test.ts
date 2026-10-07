import { describe, expect, it } from 'vitest';
import { stockVoucherSchema, type StockVoucherFormValues, type StockLineFormValues } from './schema';

const WAREHOUSE = '11111111-1111-1111-1111-111111111111';
const REASON = '22222222-2222-2222-2222-222222222222';
const PRODUCT = '33333333-3333-3333-3333-333333333333';

function line(overrides: Partial<StockLineFormValues> = {}): StockLineFormValues {
  return {
    _uiKey: 'k1',
    sortOrder: 0,
    productId: PRODUCT,
    productCode: 'SP01',
    productName: 'Tôn lạnh',
    pricingMode: 'PerUnit',
    unitName: 'Cái',
    trackInventory: true,
    priceIncludesVat: false,
    warehouseId: WAREHOUSE,
    sheetCount: '',
    length: '',
    width: '',
    thickness: '',
    quantity: 2,
    unitPrice: 100_000,
    discountRate: 0,
    discountAmount: '',
    discountManual: false,
    vatRate: 10,
    note: '',
    ...overrides,
  };
}

function voucher(overrides: Partial<StockVoucherFormValues> = {}): StockVoucherFormValues {
  return {
    voucherAt: '2026-10-06T09:00',
    warehouseId: WAREHOUSE,
    reasonId: REASON,
    partnerId: '',
    partnerName: '',
    partnerAddress: '',
    partnerTaxCode: '',
    handlerName: '',
    paymentMethodId: '',
    note: '',
    freight: 0,
    orderDiscount: 0,
    paidAmount: undefined,
    paidAmountTouched: false,
    version: undefined,
    lines: [line()],
    ...overrides,
  };
}

function issuePaths(values: StockVoucherFormValues): string[] {
  const result = stockVoucherSchema.safeParse(values);
  return result.success ? [] : result.error.issues.map((i) => i.path.join('.'));
}

describe('stockVoucherSchema', () => {
  it('requires width for square-meter lines', () => {
    const paths = issuePaths(
      voucher({ lines: [line({ pricingMode: 'PerSquareMeter', sheetCount: 2, length: 2000, width: '' })] }),
    );
    expect(paths).toContain('lines.0.width');
  });

  it('requires quantity > 0 for per-unit lines', () => {
    const result = stockVoucherSchema.safeParse(voucher({ lines: [line({ quantity: 0 })] }));
    expect(result.success).toBe(false);
    const issue = result.error?.issues.find((i) => i.path.join('.') === 'lines.0.quantity');
    expect(issue?.message).toBe('Số lượng phải lớn hơn 0');
  });

  it('requires at least one line', () => {
    const result = stockVoucherSchema.safeParse(voucher({ lines: [] }));
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((i) => i.message)).toContain('Phiếu phải có ít nhất 1 dòng');
  });

  it('rejects a manual discount above the amount', () => {
    // Amount = 2 × 100,000 = 200,000.
    const result = stockVoucherSchema.safeParse(
      voucher({ lines: [line({ discountManual: true, discountAmount: 200_001 })] }),
    );
    expect(result.success).toBe(false);
    const issue = result.error?.issues.find((i) => i.path.join('.') === 'lines.0.discountAmount');
    expect(issue?.message).toBe('Tiền CK không vượt quá số tiền');
  });

  it('accepts a valid voucher', () => {
    const result = stockVoucherSchema.safeParse(
      voucher({
        lines: [
          line({ discountManual: true, discountAmount: 200_000 }),
          line({
            _uiKey: 'k2',
            sortOrder: 1,
            pricingMode: 'PerCubicMeter',
            unitName: 'm³',
            sheetCount: 1,
            length: 1000,
            width: 50,
            thickness: 10,
            quantity: 0,
          }),
        ],
      }),
    );
    expect(result.success).toBe(true);
  });
});
