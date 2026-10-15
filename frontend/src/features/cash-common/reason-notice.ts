import {
  partnerFitsReason,
  partnerRoleLabel,
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
// An empty reason next to a picked partner of known roles means no reason fits it (§4.2 step 3): red as well.
export function reasonNoticeOf(
  partner: (PartnerRoles & { code: string }) | null | undefined,
  reason: (ReasonRoleInfo & { name: string }) | undefined,
  switchNotice: string | null,
): ReasonNotice | null {
  if (partner && !reason && partnerRolesKnown(partner)) {
    return { text: `Không có lý do nào hợp với ${partner.code} (${partnerRoleLabel(partner)})`, tone: 'error' };
  }
  const mismatch =
    partner && reason && partnerRolesKnown(partner) && !partnerFitsReason(partner, reason)
      ? reasonMismatchMessage(reason, partner.code)
      : undefined;
  if (mismatch) return { text: mismatch, tone: 'error' };
  return switchNotice ? { text: switchNotice, tone: 'info' } : null;
}
