import { describe, expect, it } from 'vitest';
import { noReasonFits, reasonNoticeOf } from './reason-notice';

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
  });

  it('says no reason fits only when the reason is empty and noReasonFits is set (section-01 §4.2 step 3)', () => {
    expect(reasonNoticeOf(supplier, undefined, null, true)).toEqual({
      text: 'Không có lý do nào hợp với NCC01 (nhà cung cấp)',
      tone: 'error',
    });
    expect(reasonNoticeOf({ code: 'KH01', isCustomer: true, isSupplier: true }, undefined, null, true)?.text).toBe(
      'Không có lý do nào hợp với KH01 (khách hàng và nhà cung cấp)',
    );
    expect(reasonNoticeOf(supplier, undefined, null)).toBeNull();
    expect(reasonNoticeOf(supplier, undefined, null, false)).toBeNull();
  });

  it('says nothing about an empty reason without a partner or with unknown roles', () => {
    expect(reasonNoticeOf(null, undefined, null, true)).toBeNull();
    expect(reasonNoticeOf({ code: 'NCC01' }, undefined, null, true)).toBeNull();
  });

  it('a chosen reason ignores noReasonFits', () => {
    expect(reasonNoticeOf(supplier, supplierReason, null, true)).toBeNull();
  });

  it('gives no mismatch for a partner whose roles are unknown (a saved voucher)', () => {
    expect(reasonNoticeOf({ code: 'NCC01' }, customerReason, null)).toBeNull();
  });

  it('gives no mismatch text for a reason that needs no particular role', () => {
    const anyReason = { name: 'Khác', partnerType: 'Any', debtEffect: 'None' } as const;
    expect(reasonNoticeOf(supplier, anyReason, null)).toBeNull();
  });
});

describe('noReasonFits', () => {
  const noneReason = { name: 'Chi khác', partnerType: 'None', debtEffect: 'None' } as const;
  const anyReason = { name: 'Khác', partnerType: 'Any', debtEffect: 'None' } as const;

  it('is true when the reasons are loaded and none fits a partner of known roles', () => {
    expect(noReasonFits(supplier, [customerReason, noneReason], true)).toBe(true);
  });

  it('is false while the reasons load, when the list is empty, or when one fits', () => {
    expect(noReasonFits(supplier, [customerReason], false)).toBe(false);
    expect(noReasonFits(supplier, [], true)).toBe(false);
    expect(noReasonFits(supplier, [customerReason, supplierReason], true)).toBe(false);
    expect(noReasonFits(supplier, [customerReason, anyReason], true)).toBe(false);
  });

  it('is false without a partner or with unknown roles', () => {
    expect(noReasonFits(null, [customerReason], true)).toBe(false);
    expect(noReasonFits({ code: 'NCC01' }, [customerReason], true)).toBe(false);
  });
});
