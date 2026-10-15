import { describe, expect, it } from 'vitest';
import { cashVoucherSchema, type CashLineFormValues, type CashVoucherFormValues } from './schema';

const FUND = '11111111-1111-1111-1111-111111111111';
const REASON = '22222222-2222-2222-2222-222222222222';
const PARTNER = '33333333-3333-3333-3333-333333333333';
const ENTRY = '44444444-4444-4444-4444-444444444444';

function line(overrides: Partial<CashLineFormValues> = {}): CashLineFormValues {
  return { _uiKey: 'k1', description: 'Thu tiền hàng', amount: 1_000, note: '', ...overrides };
}

function voucher(overrides: Partial<CashVoucherFormValues> = {}): CashVoucherFormValues {
  return {
    type: 'Receipt',
    voucherAt: '2026-10-06T09:00',
    fundId: FUND,
    reasonId: REASON,
    partnerId: '',
    partnerName: '',
    partnerAddress: '',
    personName: '',
    description: '',
    attachedDocs: '',
    note: '',
    version: undefined,
    lines: [line()],
    allocations: [],
    allocationsTouched: false,
    ...overrides,
  };
}

function issuePaths(values: CashVoucherFormValues): string[] {
  const result = cashVoucherSchema.safeParse(values);
  return result.success ? [] : result.error.issues.map((issue) => issue.path.join('.'));
}

describe('cashVoucherSchema', () => {
  it('requires one line with amount > 0', () => {
    expect(issuePaths(voucher({ lines: [] }))).toEqual(['lines']);
    expect(issuePaths(voucher({ lines: [line({ amount: 0 })] }))).toEqual(['lines.0.amount']);
    expect(issuePaths(voucher({ lines: [line({ amount: -1 })] }))).toEqual(['lines.0.amount']);
    expect(issuePaths(voucher({ lines: [line({ description: '  ' })] }))).toEqual(['lines.0.description']);
  });

  it('fund and reason required', () => {
    expect(issuePaths(voucher({ fundId: '', reasonId: '' }))).toEqual(['fundId', 'reasonId']);
  });

  it('valid voucher passes', () => {
    const result = cashVoucherSchema.parse(
      voucher({
        partnerId: PARTNER,
        description: 'Thu nợ tháng 10',
        lines: [line({ amount: '2500' })],
        allocations: [{ entryId: ENTRY, docCode: 'PX00001', amount: 0 }],
        allocationsTouched: true,
        version: 7,
      }),
    );

    expect(result.partnerId).toBe(PARTNER);
    expect(result.partnerName).toBeUndefined();
    expect(result.description).toBe('Thu nợ tháng 10');
    expect(result.lines).toEqual([{ _uiKey: 'k1', description: 'Thu tiền hàng', amount: 2_500, note: undefined }]);
    expect(result.allocations).toEqual([{ entryId: ENTRY, docCode: 'PX00001', amount: 0 }]);
    expect(result.version).toBe(7);
  });

  it('does not know partnerTaxCode', () => {
    const result = cashVoucherSchema.parse({ ...voucher(), partnerTaxCode: '0101234567' });

    expect(result).not.toHaveProperty('partnerTaxCode');
  });

  it('rejects negative allocations and over-long texts', () => {
    expect(
      issuePaths(
        voucher({
          description: 'x'.repeat(501),
          attachedDocs: 'x'.repeat(256),
          allocations: [{ entryId: ENTRY, docCode: 'PX00001', amount: -1 }],
        }),
      ),
    ).toEqual(['description', 'attachedDocs', 'allocations.0.amount']);
  });
});
