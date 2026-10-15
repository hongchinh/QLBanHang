import { describe, expect, it } from 'vitest';
import { reasonNoticeOf } from './reason-notice';

const supplierReason = { name: 'Nhập mua hàng', partnerType: 'Supplier', debtEffect: 'PayableIncrease' } as const;
const customerReason = { name: 'Nhập hàng trả lại', partnerType: 'Customer', debtEffect: 'ReceivableDecrease' } as const;
const supplier = { code: 'NCC01', isCustomer: false, isSupplier: true };

describe('reasonNoticeOf', () => {
  it('is the red mismatch when the roles are known and the partner does not fit', () => {
    expect(reasonNoticeOf(supplier, customerReason, null)).toEqual({
      text: 'Lý do "Nhập hàng trả lại" cần khách hàng — NCC01 không phải khách hàng',
      tone: 'error',
    });
  });

  it('the mismatch wins over the switch notice', () => {
    expect(reasonNoticeOf(supplier, customerReason, 'Đã đổi lý do')?.tone).toBe('error');
  });

  it('is the switch notice when the partner fits', () => {
    expect(reasonNoticeOf(supplier, supplierReason, 'Đã đổi lý do')).toEqual({ text: 'Đã đổi lý do', tone: 'info' });
  });

  it('is null with nothing to say', () => {
    expect(reasonNoticeOf(supplier, supplierReason, null)).toBeNull();
    expect(reasonNoticeOf(null, supplierReason, null)).toBeNull();
    expect(reasonNoticeOf(supplier, undefined, null)).toBeNull();
  });

  it('gives no mismatch for a partner whose roles are unknown (a saved voucher)', () => {
    expect(reasonNoticeOf({ code: 'NCC01' }, customerReason, null)).toBeNull();
  });

  it('gives no mismatch text for a reason that needs no particular role', () => {
    const anyReason = { name: 'Khác', partnerType: 'Any', debtEffect: 'None' } as const;
    expect(reasonNoticeOf(supplier, anyReason, null)).toBeNull();
  });
});
