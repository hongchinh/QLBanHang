import type { CashVoucherType, DebtEffect } from '@/features/cash-common/types';
import type { PartnerType } from '@/features/stock-reasons/types';

export interface CashReason {
  id: string;
  code: string;
  name: string;
  type: CashVoucherType;
  partnerType: PartnerType;
  debtEffect: DebtEffect;
  isSystem: boolean;
  isAuto: boolean;
}

export interface CashReasonListParams {
  type?: CashVoucherType;
  includeAuto?: boolean;
}

export interface CreateCashReasonRequest {
  code: string;
  name: string;
  type: CashVoucherType;
  partnerType: PartnerType;
  debtEffect: DebtEffect;
}

export interface UpdateCashReasonRequest {
  name: string;
  type: CashVoucherType;
  partnerType: PartnerType;
  debtEffect: DebtEffect;
}

// The debt effects a reason may carry per voucher type (backend DebtEffectRules, P19).
export const CASH_REASON_DEBT_EFFECTS: Record<CashVoucherType, DebtEffect[]> = {
  Receipt: ['None', 'ReceivableDecrease', 'PayableIncrease'],
  Payment: ['None', 'PayableDecrease', 'ReceivableIncrease'],
};
