import { getApiError } from '@/lib/api-client';

// Request flags the server accepts to skip a confirmable 422 (P10).
export type ConfirmFlag =
  | 'acknowledgeNegativeStock'
  | 'confirmRemoveSettlements'
  | 'acknowledgeCreditLimit'
  | 'acknowledgeNegativeFund';
export type ConfirmFlags = Partial<Record<ConfirmFlag, boolean>> & { confirmedSettlements?: string };

export interface ConfirmationItem {
  key: string;
  messages: string[];
}

export interface ConfirmationRequest {
  code: string;
  flag?: ConfirmFlag; // undefined when blocked
  blocked: boolean;
  title: string;
  message: string;
  items: ConfirmationItem[];
  // The settlements the server listed; resent so it can tell the list is still the one confirmed.
  token?: string;
}

interface CodeRule {
  flag?: ConfirmFlag;
  title: string;
}

// The 422 body has no canConfirm: a code is blocked when it has no flag (the _BLOCKED codes).
const CODE_RULES: Record<string, CodeRule> = {
  NEGATIVE_STOCK_WARNING: { flag: 'acknowledgeNegativeStock', title: 'Xuất quá tồn kho' },
  NEGATIVE_STOCK_BLOCKED: { title: 'Không đủ tồn kho' },
  DEBT_SETTLEMENTS_WILL_BE_REMOVED: { flag: 'confirmRemoveSettlements', title: 'Bỏ đối trừ chứng từ' },
  CREDIT_LIMIT_WARNING: { flag: 'acknowledgeCreditLimit', title: 'Vượt hạn mức nợ' },
  CREDIT_LIMIT_BLOCKED: { title: 'Vượt hạn mức nợ' },
  NEGATIVE_FUND_WARNING: { flag: 'acknowledgeNegativeFund', title: 'Quỹ bị âm' },
  NEGATIVE_FUND_BLOCKED: { title: 'Không đủ tiền trong quỹ' },
};

/** Returns null unless the error is a 422 with one of the seven known codes. */
export function toConfirmationRequest(error: unknown): ConfirmationRequest | null {
  const apiError = getApiError(error);
  if (!apiError || apiError.status !== 422) return null;
  const rule = Object.hasOwn(CODE_RULES, apiError.code) ? CODE_RULES[apiError.code] : undefined;
  if (!rule) return null;
  return {
    code: apiError.code,
    flag: rule.flag,
    blocked: rule.flag === undefined,
    title: rule.title,
    message: apiError.message,
    items: Object.entries(apiError.details ?? {}).map(([key, messages]) => ({ key, messages })),
    ...(apiError.token != null ? { token: apiError.token } : {}),
  };
}

/** Adds the request's flag to the flags already confirmed. */
export function withConfirmed(flags: ConfirmFlags, request: ConfirmationRequest): ConfirmFlags {
  if (!request.flag) return { ...flags };
  const confirmed: ConfirmFlags = { ...flags, [request.flag]: true };
  if (request.flag === 'confirmRemoveSettlements') {
    // The latest 422 wins: a token left from an earlier, different list must not ride along.
    // No token -> the server treats the confirmation as covering all settlements (legacy);
    // the new server always sends one.
    if (request.token != null) confirmed.confirmedSettlements = request.token;
    else delete confirmed.confirmedSettlements;
  }
  return confirmed;
}
