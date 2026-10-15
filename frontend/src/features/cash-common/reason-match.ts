import type { PartnerType } from '@/features/stock-reasons/types';
import type { DebtEffect } from './types';

export type RequiredRole = 'none' | 'customer' | 'supplier' | 'any';
export interface ReasonRoleInfo { partnerType: PartnerType; debtEffect: DebtEffect }
export interface PartnerRoles { isCustomer?: boolean; isSupplier?: boolean }
export type ReasonMatch = { kind: 'keep' } | { kind: 'switch'; reasonId: string } | { kind: 'none' };

/**
 * The role a partner needs for a reason. Mirrors ValidatePartner in CashVoucherService and StockVoucherService
 * (partner type of the reason, then the side role of its debt effect).
 */
export function requiredRole(reason: ReasonRoleInfo): RequiredRole {
  switch (reason.partnerType) {
    case 'None': return 'none';
    case 'Customer': return 'customer';
    case 'Supplier': return 'supplier';
    default:
      // Explicit values: a mock or old payload without debtEffect must not throw.
      if (reason.debtEffect === 'ReceivableDecrease' || reason.debtEffect === 'ReceivableIncrease') return 'customer';
      if (reason.debtEffect === 'PayableDecrease' || reason.debtEffect === 'PayableIncrease') return 'supplier';
      return 'any';
  }
}

export function partnerFitsReason(partner: PartnerRoles, reason: ReasonRoleInfo | undefined): boolean {
  if (!reason) return false;
  switch (requiredRole(reason)) {
    case 'none': return false;
    case 'any': return true;
    case 'customer': return partner.isCustomer === true;
    default: return partner.isSupplier === true;
  }
}

export function matchReasonForPartner<R extends ReasonRoleInfo & { id: string }>(
  partner: PartnerRoles, currentReasonId: string, reasons: readonly R[],
): ReasonMatch {
  const current = reasons.find((r) => r.id === currentReasonId);
  if (current && partnerFitsReason(partner, current)) return { kind: 'keep' };
  const withRole = reasons.find((r) => {
    const role = requiredRole(r);
    return (role === 'customer' || role === 'supplier') && partnerFitsReason(partner, r);
  });
  if (withRole) return { kind: 'switch', reasonId: withRole.id };
  const plain = reasons.find((r) => requiredRole(r) === 'any');
  return plain ? { kind: 'switch', reasonId: plain.id } : { kind: 'none' };
}

// The role a quick add starts with: what the reason requires, else the voucher direction
// (receipt / stock-out -> customer, payment / stock-in -> supplier).
export function preferredPartnerRole(reason: ReasonRoleInfo | undefined, fallback: 'customer' | 'supplier'): 'customer' | 'supplier' {
  const role = reason ? requiredRole(reason) : 'any';
  return role === 'customer' || role === 'supplier' ? role : fallback;
}

export function partnerRolesKnown(partner: PartnerRoles | null | undefined): boolean {
  return typeof partner?.isCustomer === 'boolean' && typeof partner?.isSupplier === 'boolean';
}

export function partnerRoleLabel(partner: PartnerRoles): string {
  if (partner.isCustomer && partner.isSupplier) return 'khách hàng và nhà cung cấp';
  if (partner.isCustomer) return 'khách hàng';
  if (partner.isSupplier) return 'nhà cung cấp';
  return 'đối tượng';
}

export function reasonSwitchMessage(partnerCode: string, partner: PartnerRoles): string {
  return `Đã đổi lý do cho hợp với ${partnerCode} (${partnerRoleLabel(partner)}).`;
}

// Text only; the caller decides when to show it (roles known and the partner does not fit).
export function reasonMismatchMessage(reason: ReasonRoleInfo & { name: string }, partnerCode: string): string | undefined {
  const role = requiredRole(reason);   // never reason.partnerType: an "any" reason with a debt effect needs a side role
  if (role !== 'customer' && role !== 'supplier') return undefined;
  const label = role === 'customer' ? 'khách hàng' : 'nhà cung cấp';
  return `Lý do "${reason.name}" cần ${label} — ${partnerCode} không phải ${label}`;
}
