import { describe, expect, it } from 'vitest';
import { voucherPrintFileBase } from './types';

const now = new Date(2026, 9, 14, 16, 5);
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
});
