import { describe, expect, it } from 'vitest';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { fundTransferSchema } from './schema';
import { toFormDefaults, toUpsertPayload } from './payload';
import type { FundTransfer } from './types';

const F1 = '11111111-1111-4111-8111-111111111111';
const F2 = '22222222-2222-4222-8222-222222222222';
const AT = '2026-10-07T01:30:00.000Z';

function transfer(overrides: Partial<FundTransfer> = {}): FundTransfer {
  return {
    id: '33333333-3333-4333-8333-333333333333',
    code: 'CQ00003',
    voucherAt: AT,
    branchId: 'b1',
    fromFundId: F1,
    fromFundCode: 'QTM',
    fromFundName: 'Quỹ tiền mặt',
    fromFundKind: 'Cash',
    toFundId: F2,
    toFundCode: 'VCB',
    toFundName: 'Vietcombank',
    toFundKind: 'Bank',
    amount: 1_500_000,
    description: 'Nộp tiền vào ngân hàng',
    note: null,
    status: 'Active',
    cancelledAt: null,
    ownerUserId: 'u1',
    ownerName: 'Kế toán A',
    version: 9,
    canEdit: true,
    canCancel: true,
    canDelete: true,
    ...overrides,
  };
}

describe('fund transfer payload', () => {
  it('maps payload with flags', () => {
    const parsed = fundTransferSchema.parse({
      voucherAt: toDateTimeLocalValue(AT),
      fromFundId: F1,
      toFundId: F2,
      amount: '1.500.000',
      description: 'Nộp tiền vào ngân hàng',
      note: '',
      version: 9,
    });

    expect(toUpsertPayload(parsed)).toEqual({
      voucherAt: AT,
      fromFundId: F1,
      toFundId: F2,
      amount: 1_500_000,
      description: 'Nộp tiền vào ngân hàng',
      note: undefined,
      version: 9,
      acknowledgeNegativeFund: false,
    });
    expect(toUpsertPayload(parsed, { acknowledgeNegativeFund: true })).toMatchObject({
      acknowledgeNegativeFund: true,
    });
  });

  it('rejects same funds in schema', () => {
    const result = fundTransferSchema.safeParse({
      voucherAt: toDateTimeLocalValue(AT),
      fromFundId: F1,
      toFundId: F1,
      amount: 1000,
      description: '',
      note: '',
    });

    expect(result.success).toBe(false);
    if (result.success) return;
    expect(result.error.issues).toContainEqual(
      expect.objectContaining({ path: ['toFundId'], message: 'Quỹ đến phải khác quỹ đi' }),
    );
  });

  it('rejects a zero amount', () => {
    const result = fundTransferSchema.safeParse({
      voucherAt: toDateTimeLocalValue(AT),
      fromFundId: F1,
      toFundId: F2,
      amount: '0',
    });

    expect(result.success).toBe(false);
    if (result.success) return;
    expect(result.error.issues).toContainEqual(expect.objectContaining({ path: ['amount'] }));
  });

  it('form defaults for a new and an existing transfer', () => {
    expect(toFormDefaults(undefined, { nextCode: 'CQ00001', voucherAt: AT })).toEqual({
      voucherAt: toDateTimeLocalValue(AT),
      fromFundId: '',
      toFundId: '',
      amount: '',
      description: '',
      note: '',
      version: undefined,
    });
    expect(toFormDefaults(transfer())).toEqual({
      voucherAt: toDateTimeLocalValue(AT),
      fromFundId: F1,
      toFundId: F2,
      amount: '1.500.000',
      description: 'Nộp tiền vào ngân hàng',
      note: '',
      version: 9,
    });
  });
});
