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
// An empty reason with `noReasonFits` (see below) is red as well (§4.2 step 3).
export function reasonNoticeOf(
  partner: (PartnerRoles & { code: string }) | null | undefined,
  reason: (ReasonRoleInfo & { name: string }) | undefined,
  switchNotice: string | null,
  noReasonFits = false,
): ReasonNotice | null {
  if (partner && !reason && noReasonFits && partnerRolesKnown(partner)) {
    return { text: `Không có lý do nào hợp với ${partner.code} (${partnerRoleLabel(partner)})`, tone: 'error' };
  }
  const mismatch =
    partner && reason && partnerRolesKnown(partner) && !partnerFitsReason(partner, reason)
      ? reasonMismatchMessage(reason, partner.code)
      : undefined;
  if (mismatch) return { text: mismatch, tone: 'error' };
  return switchNotice ? { text: switchNotice, tone: 'info' } : null;
}

// No reason of the list the select shows fits a picked partner of known roles. False while the reasons load and for
// an empty list (a configuration gap, section-01 §9): an empty select then says nothing about the partner.
export function noReasonFits(
  partner: PartnerRoles | null | undefined,
  reasons: readonly ReasonRoleInfo[],
  loaded: boolean,
): boolean {
  return (
    loaded &&
    reasons.length > 0 &&
    !!partner &&
    partnerRolesKnown(partner) &&
    !reasons.some((r) => partnerFitsReason(partner, r))
  );
}
