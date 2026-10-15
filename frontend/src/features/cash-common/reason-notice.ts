import {
  partnerFitsReason,
  partnerRolesKnown,
  reasonMismatchMessage,
  type PartnerRoles,
  type ReasonRoleInfo,
} from './reason-match';

export interface ReasonNotice {
  text: string;
  tone: 'info' | 'error';
}

// The line under the reason select of a voucher form (section-01 §4.3, P7): the red mismatch is derived from the
// partner and the reason (roles known, partner does not fit), else the yellow "reason changed" text, else nothing.
export function reasonNoticeOf(
  partner: (PartnerRoles & { code: string }) | null | undefined,
  reason: (ReasonRoleInfo & { name: string }) | undefined,
  switchNotice: string | null,
): ReasonNotice | null {
  const mismatch =
    partner && reason && partnerRolesKnown(partner) && !partnerFitsReason(partner, reason)
      ? reasonMismatchMessage(reason, partner.code)
      : undefined;
  if (mismatch) return { text: mismatch, tone: 'error' };
  return switchNotice ? { text: switchNotice, tone: 'info' } : null;
}
