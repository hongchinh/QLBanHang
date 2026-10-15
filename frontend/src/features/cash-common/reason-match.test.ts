import { describe, expect, it } from 'vitest';
import {
  matchReasonForPartner, partnerFitsReason, partnerRoleLabel, partnerRolesKnown, preferredPartnerRole,
  reasonMismatchMessage, reasonSwitchMessage, requiredRole, type ReasonRoleInfo,
} from './reason-match';

const info = (partnerType: ReasonRoleInfo['partnerType'], debtEffect: ReasonRoleInfo['debtEffect'] = 'None'): ReasonRoleInfo =>
  ({ partnerType, debtEffect });
const reason = (id: string, partnerType: ReasonRoleInfo['partnerType'], debtEffect: ReasonRoleInfo['debtEffect'] = 'None') =>
  ({ id, partnerType, debtEffect });

describe('requiredRole (section-01 §4.1)', () => {
  it.each([
    // None+ReceivableIncrease, Customer+PayableIncrease and Supplier+ReceivableDecrease cannot be saved
    // (DebtEffectRules.IsAllowedPartnerType); they only pin "partner type wins".
    ['None', 'None', 'none'], ['None', 'ReceivableIncrease', 'none'],
    ['Customer', 'None', 'customer'], ['Customer', 'PayableIncrease', 'customer'],
    ['Supplier', 'None', 'supplier'], ['Supplier', 'ReceivableDecrease', 'supplier'],
    ['Any', 'None', 'any'],
    ['Any', 'ReceivableDecrease', 'customer'], ['Any', 'ReceivableIncrease', 'customer'],
    ['Any', 'PayableDecrease', 'supplier'], ['Any', 'PayableIncrease', 'supplier'],
  ] as const)('%s + %s -> %s', (partnerType, debtEffect, expected) => {
    expect(requiredRole(info(partnerType, debtEffect))).toBe(expected);
  });
});

describe('partnerFitsReason', () => {
  it('an empty reason never fits', () => expect(partnerFitsReason({ isCustomer: true }, undefined)).toBe(false));
  it('a reason without partner never fits', () => expect(partnerFitsReason({ isCustomer: true, isSupplier: true }, info('None'))).toBe(false));
  it('an "any" reason fits everyone, even unknown roles', () => expect(partnerFitsReason({}, info('Any'))).toBe(true));
  it('a customer reason needs isCustomer', () => {
    expect(partnerFitsReason({ isCustomer: true }, info('Customer'))).toBe(true);
    expect(partnerFitsReason({ isSupplier: true }, info('Customer'))).toBe(false);
    expect(partnerFitsReason({}, info('Customer'))).toBe(false);
  });
  it('a supplier reason needs isSupplier', () => {
    expect(partnerFitsReason({ isSupplier: true }, info('Supplier'))).toBe(true);
    expect(partnerFitsReason({ isCustomer: true }, info('Supplier'))).toBe(false);
  });
  it('"any" with a payable effect needs a supplier', () => {
    expect(partnerFitsReason({ isCustomer: true }, info('Any', 'PayableIncrease'))).toBe(false);
    expect(partnerFitsReason({ isSupplier: true }, info('Any', 'PayableIncrease'))).toBe(true);
  });
});

describe('matchReasonForPartner (section-01 §4.2)', () => {
  const none = reason('none', 'None');
  const tkh = reason('tkh', 'Customer');
  const tncc = reason('tncc', 'Supplier');
  const other = reason('other', 'Any');
  const anyRecv = reason('any-recv', 'Any', 'ReceivableDecrease');
  const customer = { isCustomer: true, isSupplier: false };
  const supplier = { isCustomer: false, isSupplier: true };
  const both = { isCustomer: true, isSupplier: true };

  it('keeps a reason that fits', () =>
    expect(matchReasonForPartner(customer, 'tkh', [none, tkh, tncc, other])).toEqual({ kind: 'keep' }));
  it('keeps an "any" reason that fits', () =>
    expect(matchReasonForPartner(customer, 'other', [none, tkh, tncc, other])).toEqual({ kind: 'keep' }));
  it('switches to the first customer reason', () =>
    expect(matchReasonForPartner(customer, 'tncc', [none, tkh, tncc, other])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
  it('switches to the first supplier reason', () =>
    expect(matchReasonForPartner(supplier, 'tkh', [none, tkh, tncc, other])).toEqual({ kind: 'switch', reasonId: 'tncc' }));
  it('an empty reason counts as not fitting', () =>
    expect(matchReasonForPartner(customer, '', [none, tkh, tncc])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
  it('a "no partner" reason does not fit', () =>
    expect(matchReasonForPartner(customer, 'none', [none, tkh])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
  it('prefers a role reason over an earlier plain "any" reason', () =>
    expect(matchReasonForPartner(customer, '', [other, tkh])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
  it('counts an "any" reason with a receivable effect as a customer reason', () =>
    expect(matchReasonForPartner(customer, '', [other, anyRecv])).toEqual({ kind: 'switch', reasonId: 'any-recv' }));
  it('falls back to a plain "any" reason', () =>
    expect(matchReasonForPartner(customer, 'tncc', [tncc, other])).toEqual({ kind: 'switch', reasonId: 'other' }));
  it('a partner with both roles keeps a supplier reason and otherwise takes the first role reason', () => {
    expect(matchReasonForPartner(both, 'tncc', [tkh, tncc])).toEqual({ kind: 'keep' });
    expect(matchReasonForPartner(both, '', [tkh, tncc])).toEqual({ kind: 'switch', reasonId: 'tkh' });
  });
  it('reports none when nothing fits', () => {
    expect(matchReasonForPartner(customer, 'none', [none])).toEqual({ kind: 'none' });
    expect(matchReasonForPartner(customer, 'tncc', [none, tncc])).toEqual({ kind: 'none' });
  });
  it('a partner without roles only fits a plain "any" reason', () => {
    expect(matchReasonForPartner({}, 'tkh', [tkh, other])).toEqual({ kind: 'switch', reasonId: 'other' });
    expect(matchReasonForPartner({ isCustomer: false, isSupplier: false }, 'tkh', [tkh])).toEqual({ kind: 'none' });
    expect(matchReasonForPartner({}, 'other', [tkh, other])).toEqual({ kind: 'keep' });
  });
});

describe('preferredPartnerRole (the role a quick add starts with, section-01 §6.2)', () => {
  it('follows the role a reason requires', () => {
    expect(preferredPartnerRole(info('Customer'), 'supplier')).toBe('customer');
    expect(preferredPartnerRole(info('Supplier'), 'customer')).toBe('supplier');
    expect(preferredPartnerRole(info('Any', 'PayableIncrease'), 'customer')).toBe('supplier');
  });
  it('uses the voucher direction for a plain "any", a "no partner" or a missing reason', () => {
    expect(preferredPartnerRole(info('Any'), 'supplier')).toBe('supplier');
    expect(preferredPartnerRole(info('None'), 'customer')).toBe('customer');
    expect(preferredPartnerRole(undefined, 'supplier')).toBe('supplier');
  });
});

describe('messages', () => {
  it('partnerRolesKnown needs both flags', () => {
    expect(partnerRolesKnown({ isCustomer: true, isSupplier: false })).toBe(true);
    expect(partnerRolesKnown({ isCustomer: true })).toBe(false);
    expect(partnerRolesKnown(null)).toBe(false);
  });
  it('partnerRoleLabel', () => {
    expect(partnerRoleLabel({ isCustomer: true })).toBe('khách hàng');
    expect(partnerRoleLabel({ isSupplier: true })).toBe('nhà cung cấp');
    expect(partnerRoleLabel({ isCustomer: true, isSupplier: true })).toBe('khách hàng và nhà cung cấp');
    expect(partnerRoleLabel({})).toBe('đối tượng');
  });
  it('reasonSwitchMessage', () =>
    expect(reasonSwitchMessage('KH0001', { isCustomer: true })).toBe('Đã đổi lý do cho hợp với KH0001 (khách hàng).'));
  it('reasonMismatchMessage names the needed role', () => {
    expect(reasonMismatchMessage({ name: 'Thu tiền khách hàng', ...info('Customer') }, 'NCC001'))
      .toBe('Lý do "Thu tiền khách hàng" cần khách hàng — NCC001 không phải khách hàng');
    expect(reasonMismatchMessage({ name: 'Chi trả NCC', ...info('Supplier') }, 'KH01'))
      .toBe('Lý do "Chi trả NCC" cần nhà cung cấp — KH01 không phải nhà cung cấp');
  });
  it('reasonMismatchMessage follows the debt side of an "any" reason', () => {
    expect(reasonMismatchMessage({ name: 'Thu nợ khác', ...info('Any', 'ReceivableDecrease') }, 'NCC001'))
      .toBe('Lý do "Thu nợ khác" cần khách hàng — NCC001 không phải khách hàng');
    expect(reasonMismatchMessage({ name: 'Thu hoàn khác', ...info('Any', 'PayableIncrease') }, 'KH01'))
      .toBe('Lý do "Thu hoàn khác" cần nhà cung cấp — KH01 không phải nhà cung cấp');
  });
  it('reasonMismatchMessage is empty for "any" and "no partner" reasons', () => {
    expect(reasonMismatchMessage({ name: 'Thu khác', ...info('Any') }, 'KH01')).toBeUndefined();
    expect(reasonMismatchMessage({ name: 'Không có', ...info('None') }, 'KH01')).toBeUndefined();
  });
});
