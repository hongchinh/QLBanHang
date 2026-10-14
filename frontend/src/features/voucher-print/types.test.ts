import { describe, expect, it } from 'vitest';
import { voucherPrintFileBase } from './types';

// 09:05 UTC is 16:05 in Vietnam (UTC+7).
const now = new Date(Date.UTC(2026, 9, 14, 9, 5));
const two = [
  { id: 'a', code: 'PT00001' },
  { id: 'b', code: 'PT00002' },
];

describe('voucherPrintFileBase', () => {
  it('uses the code for one voucher', () => {
    expect(voucherPrintFileBase({ kind: 'cash', type: 'Receipt', vouchers: [{ id: 'a', code: 'PT00001' }] })).toBe('PT00001');
  });

  it.each([
    ['cash', 'Receipt', 'PhieuThu'],
    ['cash', 'Payment', 'PhieuChi'],
    ['stock', 'In', 'PhieuNhapKho'],
    ['stock', 'Out', 'PhieuXuatKho'],
  ] as const)('%s %s uses prefix %s with a timestamp for several vouchers', (kind, type, prefix) => {
    expect(voucherPrintFileBase({ kind, type, vouchers: two }, now)).toBe(`${prefix}_20261014-1605`);
  });

  it('rolls the day over in Vietnam time', () => {
    const late = new Date(Date.UTC(2026, 9, 14, 18, 30));
    expect(voucherPrintFileBase({ kind: 'cash', type: 'Receipt', vouchers: two }, late)).toBe('PhieuThu_20261015-0130');
  });
});
