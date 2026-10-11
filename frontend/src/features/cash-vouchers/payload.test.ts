import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { toFormDefaults, toUpsertPayload } from './payload';
import { cashVoucherSchema, type CashVoucherFormValues } from './schema';
import type { CashVoucher, CashVoucherAllocation, CashVoucherDefaults } from './types';

const originalTz = process.env.TZ;
beforeAll(() => {
  process.env.TZ = 'Asia/Ho_Chi_Minh';
});
afterAll(() => {
  process.env.TZ = originalTz;
});

const FUND = '11111111-1111-1111-1111-111111111111';
const REASON = '22222222-2222-2222-2222-222222222222';
const PARTNER = '33333333-3333-3333-3333-333333333333';
const ENTRY_1 = '44444444-4444-4444-4444-444444444441';
const ENTRY_2 = '44444444-4444-4444-4444-444444444442';
const ENTRY_3 = '44444444-4444-4444-4444-444444444443';
const LINE_ID = '55555555-5555-5555-5555-555555555555';
const VOUCHER_ID = '66666666-6666-6666-6666-666666666666';

function formValues(overrides: Partial<CashVoucherFormValues> = {}): CashVoucherFormValues {
  return {
    type: 'Receipt',
    voucherAt: '2026-10-06T09:00',
    fundId: FUND,
    reasonId: REASON,
    partnerId: PARTNER,
    partnerName: 'Công ty A',
    partnerAddress: '',
    partnerTaxCode: '',
    personName: 'Nguyễn Văn A',
    description: 'Thu nợ',
    attachedDocs: '',
    note: '',
    version: undefined,
    lines: [{ _uiKey: 'k1', description: 'Thu tiền hàng', amount: 2_500, note: '' }],
    allocations: [
      { entryId: ENTRY_1, docCode: 'PX00001', amount: 1_000 },
      { entryId: ENTRY_2, docCode: 'PX00002', amount: 1_500 },
      { entryId: ENTRY_3, docCode: 'PX00003', amount: 0 },
    ],
    allocationsTouched: false,
    ...overrides,
  };
}

function allocation(overrides: Partial<CashVoucherAllocation>): CashVoucherAllocation {
  return {
    settlementId: crypto.randomUUID(),
    entryId: ENTRY_1,
    sourceType: 'StockOut',
    sourceId: crypto.randomUUID(),
    docCode: 'PX00001',
    docDate: '2026-10-01',
    amount: 1_000,
    origin: 'AtVoucher',
    ...overrides,
  };
}

function serverVoucher(overrides: Partial<CashVoucher> = {}): CashVoucher {
  return {
    id: VOUCHER_ID,
    type: 'Receipt',
    code: 'PT00001',
    voucherAt: '2026-10-06T02:00:00Z',
    branchId: crypto.randomUUID(),
    fundId: FUND,
    fundCode: 'QTM',
    fundName: 'Quỹ tiền mặt',
    fundKind: 'Cash',
    reasonId: REASON,
    reasonCode: 'TKH',
    reasonName: 'Thu tiền khách hàng',
    reasonDebtEffect: 'ReceivableDecrease',
    partnerId: PARTNER,
    partnerCode: 'KH01',
    partnerName: 'Công ty A',
    personName: 'Nguyễn Văn A',
    total: 2_500,
    status: 'Active',
    ownerUserId: crypto.randomUUID(),
    version: 42,
    isManaged: false,
    canEdit: true,
    canCancel: true,
    canDelete: true,
    lines: [{ id: LINE_ID, sortOrder: 0, description: 'Thu tiền hàng', amount: 2_500 }],
    allocations: [
      allocation({ entryId: ENTRY_1, docCode: 'PX00001', amount: 1_000, origin: 'AtVoucher' }),
      allocation({ entryId: ENTRY_2, docCode: 'PX00002', amount: 700, origin: 'Manual' }),
      allocation({ entryId: ENTRY_3, docCode: 'BT00001', amount: 300, origin: 'Offset' }),
    ],
    ...overrides,
  };
}

describe('toUpsertPayload', () => {
  it('maps a new receipt with allocations', () => {
    const parsed = cashVoucherSchema.parse(formValues());

    expect(toUpsertPayload(parsed, 'ReceivableDecrease')).toEqual({
      type: 'Receipt',
      voucherAt: '2026-10-06T02:00:00.000Z',
      fundId: FUND,
      reasonId: REASON,
      partnerId: PARTNER,
      partnerName: 'Công ty A',
      partnerAddress: undefined,
      partnerTaxCode: undefined,
      personName: 'Nguyễn Văn A',
      description: 'Thu nợ',
      attachedDocs: undefined,
      note: undefined,
      version: undefined,
      lines: [{ id: undefined, description: 'Thu tiền hàng', amount: 2_500, note: undefined }],
      allocations: [
        { entryId: ENTRY_1, amount: 1_000 },
        { entryId: ENTRY_2, amount: 1_500 },
      ],
    });
  });

  it('sends empty allocations for reasons without debt effect', () => {
    const parsed = cashVoucherSchema.parse(formValues());

    expect(toUpsertPayload(parsed, 'None').allocations).toEqual([]);
  });

  it('flags are spread into the payload', () => {
    const parsed = cashVoucherSchema.parse(formValues());

    const payload = toUpsertPayload(parsed, 'ReceivableDecrease', {
      confirmRemoveSettlements: true,
      acknowledgeNegativeFund: true,
    });

    expect(payload.confirmRemoveSettlements).toBe(true);
    expect(payload.acknowledgeNegativeFund).toBe(true);
    expect(toUpsertPayload(parsed, 'None')).not.toHaveProperty('confirmRemoveSettlements');
  });

  it('version comes from form values', () => {
    const parsed = cashVoucherSchema.parse(
      formValues({ version: 42, lines: [{ id: LINE_ID, description: 'Thu', amount: 10, note: 'x' }] }),
    );

    const payload = toUpsertPayload(parsed, 'ReceivableDecrease');

    expect(payload.version).toBe(42);
    expect(payload.lines).toEqual([{ id: LINE_ID, description: 'Thu', amount: 10, note: 'x' }]);
  });
});

describe('toFormDefaults', () => {
  it('new voucher uses the server defaults and one empty line', () => {
    const defaults: CashVoucherDefaults = {
      nextCode: 'PC00007',
      voucherAt: '2026-10-06T02:00:00Z',
      fundId: FUND,
      reasonId: REASON,
    };

    const values = toFormDefaults(undefined, defaults, 'Payment');

    expect(values).toMatchObject({
      type: 'Payment',
      voucherAt: '2026-10-06T09:00',
      fundId: FUND,
      reasonId: REASON,
      partnerId: '',
      version: undefined,
      allocations: [],
      allocationsTouched: false,
    });
    expect(values.lines).toHaveLength(1);
    expect(values.lines[0]).toMatchObject({ description: '', amount: 0, note: '' });
  });

  it('new voucher without defaults leaves fund and reason empty', () => {
    const values = toFormDefaults(undefined, undefined, 'Receipt');

    expect(values.fundId).toBe('');
    expect(values.reasonId).toBe('');
  });

  it('edit keeps only at-voucher allocations in defaults', () => {
    const values = toFormDefaults(serverVoucher(), undefined, 'Receipt');

    expect(values.allocations).toEqual([{ entryId: ENTRY_1, docCode: 'PX00001', amount: 1_000 }]);
    // A stored voucher keeps its allocations until the user asks for a new suggestion.
    expect(values.allocationsTouched).toBe(true);
  });

  it('edit maps the stored voucher and its version', () => {
    const values = toFormDefaults(serverVoucher({ type: 'Payment' }), undefined, 'Receipt');

    expect(values).toMatchObject({
      type: 'Payment',
      voucherAt: '2026-10-06T09:00',
      fundId: FUND,
      reasonId: REASON,
      partnerId: PARTNER,
      partnerName: 'Công ty A',
      partnerAddress: '',
      personName: 'Nguyễn Văn A',
      description: '',
      version: 42,
    });
    expect(values.lines).toEqual([
      { _uiKey: LINE_ID, id: LINE_ID, description: 'Thu tiền hàng', amount: 2_500, note: '' },
    ]);
  });
});
