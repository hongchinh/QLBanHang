import type {
  CashDocumentType,
  CashVoucherType,
  DebtEffect,
  DebtSide,
  FundKind,
  LimitPolicy,
  OpeningDebtKind,
} from './types';

export const DEBT_EFFECT_LABELS: Record<DebtEffect, string> = {
  None: 'Không ảnh hưởng',
  ReceivableDecrease: 'Giảm phải thu',
  ReceivableIncrease: 'Tăng phải thu',
  PayableDecrease: 'Giảm phải trả',
  PayableIncrease: 'Tăng phải trả',
};

export const DEBT_SIDE_LABELS: Record<DebtSide, string> = {
  Receivable: 'Phải thu',
  Payable: 'Phải trả',
};

export const CASH_VOUCHER_TYPE_LABELS: Record<CashVoucherType, string> = {
  Receipt: 'Phiếu thu',
  Payment: 'Phiếu chi',
};

export const FUND_KIND_LABELS: Record<FundKind, string> = {
  None: 'Không',
  Cash: 'Tiền mặt',
  Bank: 'Ngân hàng',
};

export const LIMIT_POLICY_LABELS: Record<LimitPolicy, string> = {
  Allow: 'Cho phép',
  Warn: 'Cảnh báo',
  Block: 'Chặn',
};

export const CASH_DOCUMENT_TYPE_LABELS: Record<CashDocumentType, string> = {
  Receipt: 'Phiếu thu',
  Payment: 'Phiếu chi',
  FundTransfer: 'Chuyển quỹ',
  DebtOffset: 'Bù trừ công nợ',
};

export const OPENING_DEBT_KIND_LABELS: Record<OpeningDebtKind, string> = {
  Debt: 'Nợ',
  Prepayment: 'Trả trước',
};
