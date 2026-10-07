import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { toFormDefaults, toUpsertPayload } from './payload';
import { stockVoucherSchema, type StockVoucherFormParsed, type StockVoucherFormValues } from './schema';
import type { StockVoucher, StockVoucherDefaults, StockVoucherLine } from './types';

const originalTz = process.env.TZ;
beforeAll(() => {
  process.env.TZ = 'Asia/Ho_Chi_Minh';
});
afterAll(() => {
  process.env.TZ = originalTz;
});

const WAREHOUSE = '11111111-1111-1111-1111-111111111111';
const REASON = '22222222-2222-2222-2222-222222222222';
const PRODUCT = '33333333-3333-3333-3333-333333333333';
const LINE_ID = '44444444-4444-4444-4444-444444444444';
const VOUCHER_ID = '55555555-5555-5555-5555-555555555555';
const PAYMENT = '66666666-6666-6666-6666-666666666666';

function serverLine(overrides: Partial<StockVoucherLine> = {}): StockVoucherLine {
  return {
    id: LINE_ID,
    sortOrder: 0,
    productId: PRODUCT,
    productCode: 'SP01',
    productName: 'Tôn lạnh',
    warehouseId: WAREHOUSE,
    warehouseCode: 'KHO01',
    trackInventory: true,
    pricingMode: 'PerUnit',
    unitName: 'Cái',
    priceIncludesVat: false,
    quantity: 2,
    unitPrice: 100_000,
    amount: 200_000,
    discountRate: 0,
    discountAmount: 0,
    discountManual: false,
    orderDiscountAllocated: 0,
    freightAllocated: 0,
    vatRate: 10,
    vatAmount: 20_000,
    netAmount: 220_000,
    ...overrides,
  };
}

function serverVoucher(overrides: Partial<StockVoucher> = {}): StockVoucher {
  return {
    id: VOUCHER_ID,
    type: 'In',
    code: 'PN00001',
    voucherAt: '2026-10-06T02:00:00+00:00',
    branchId: '77777777-7777-7777-7777-777777777777',
    warehouseId: WAREHOUSE,
    warehouseCode: 'KHO01',
    warehouseName: 'Kho chính',
    reasonId: REASON,
    reasonName: 'Nhập mua hàng',
    paymentMethodId: PAYMENT,
    freight: 0,
    orderDiscount: 0,
    goodsAmount: 200_000,
    lineDiscountTotal: 0,
    discountTotal: 0,
    vatTotal: 20_000,
    total: 220_000,
    paidAmount: 100_000,
    status: 'Active',
    ownerUserId: '88888888-8888-8888-8888-888888888888',
    createdAt: '2026-10-06T02:00:00+00:00',
    version: 42,
    canEdit: true,
    canCancel: true,
    canDelete: true,
    lines: [serverLine()],
    ...overrides,
  };
}

const defaults: StockVoucherDefaults = {
  nextCode: 'PN00002',
  voucherAt: '2026-10-07T01:30:00+00:00',
  warehouseId: WAREHOUSE,
  reasonId: REASON,
  paymentMethodId: PAYMENT,
};

function parse(values: StockVoucherFormValues): StockVoucherFormParsed {
  return stockVoucherSchema.parse(values);
}

function newVoucherValues(): StockVoucherFormValues {
  const values = toFormDefaults(undefined, defaults);
  values.lines = [
    {
      ...values.lines[0],
      productId: PRODUCT,
      productCode: 'SP01',
      productName: 'Tôn lạnh',
      unitName: 'Cái',
      quantity: 3,
      unitPrice: 50_000,
      vatRate: 8,
    },
  ];
  return values;
}

describe('toUpsertPayload', () => {
  it('maps form values to request with ISO voucherAt', () => {
    const payload = toUpsertPayload('In', parse(newVoucherValues()));
    expect(payload).toMatchObject({
      type: 'In',
      voucherAt: '2026-10-07T01:30:00.000Z',
      warehouseId: WAREHOUSE,
      reasonId: REASON,
      paymentMethodId: PAYMENT,
      freight: 0,
      orderDiscount: 0,
      acknowledgeNegativeStock: false,
    });
    expect(payload.lines).toEqual([
      expect.objectContaining({
        sortOrder: 0,
        productId: PRODUCT,
        warehouseId: WAREHOUSE,
        quantity: 3,
        unitPrice: 50_000,
        discountRate: 0,
        discountManual: false,
        vatRate: 8,
      }),
    ]);
    expect(payload.lines[0]).not.toHaveProperty('_uiKey');
  });

  it('omits paidAmount for an untouched new voucher', () => {
    const values = newVoucherValues();
    values.paidAmount = 162_000;
    const payload = toUpsertPayload('In', parse(values), true);
    expect(payload.paidAmount).toBeUndefined();
    expect(payload.version).toBeUndefined();
    expect(payload.acknowledgeNegativeStock).toBe(true);
  });

  it('edit of a partially paid voucher keeps paidAmount', () => {
    const values = toFormDefaults(serverVoucher());
    expect(values.paidAmountTouched).toBe(true);
    expect(toUpsertPayload('In', parse(values)).paidAmount).toBe(100_000);
  });

  it('edit round-trip keeps voucherAt unchanged', () => {
    const values = toFormDefaults(serverVoucher());
    expect(values.voucherAt).toBe('2026-10-06T09:00');
    expect(toUpsertPayload('In', parse(values)).voucherAt).toBe('2026-10-06T02:00:00.000Z');
  });

  it('version comes from the form values', () => {
    const values = toFormDefaults(serverVoucher({ version: 42 }));
    values.version = 41;
    expect(toUpsertPayload('In', parse(values)).version).toBe(41);
  });

  it('sends zero quantity for dimension-based lines', () => {
    const values = toFormDefaults(
      serverVoucher({
        lines: [
          serverLine({
            pricingMode: 'PerSquareMeter',
            unitName: 'm²',
            sheetCount: 2,
            length: 2000,
            width: 1000,
            quantity: 4,
          }),
        ],
      }),
    );
    const [line] = toUpsertPayload('In', parse(values)).lines;
    expect(line).toMatchObject({ id: LINE_ID, quantity: 0, sheetCount: 2, length: 2000, width: 1000 });
  });
});

describe('toFormDefaults', () => {
  it('toFormDefaults uses defaults for new vouchers and voucher values for edits', () => {
    const created = toFormDefaults(undefined, defaults);
    expect(created).toMatchObject({
      voucherAt: '2026-10-07T08:30',
      warehouseId: WAREHOUSE,
      reasonId: REASON,
      paymentMethodId: PAYMENT,
      paidAmountTouched: false,
      version: undefined,
    });
    expect(created.lines).toHaveLength(1);
    expect(created.lines[0].warehouseId).toBe(WAREHOUSE);

    const edited = toFormDefaults(serverVoucher({ paidAmount: 220_000 }), defaults);
    expect(edited).toMatchObject({
      voucherAt: '2026-10-06T09:00',
      version: 42,
      paidAmount: 220_000,
      paidAmountTouched: false,
    });
    expect(edited.lines[0]).toMatchObject({ _uiKey: LINE_ID, id: LINE_ID, quantity: 2, unitPrice: 100_000 });
  });
});
