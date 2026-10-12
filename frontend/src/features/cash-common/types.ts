// Shared cash and debt types (Round 2A). Enums are serialized by name, as in the backend
// Domain/Enums/CashDebtEnums.cs.

export type DebtEffect =
  | 'None'
  | 'ReceivableDecrease'
  | 'ReceivableIncrease'
  | 'PayableDecrease'
  | 'PayableIncrease';

export type DebtSide = 'Receivable' | 'Payable';

export type DebtDirection = 'Increase' | 'Decrease';

export type CashVoucherType = 'Receipt' | 'Payment';

export type FundKind = 'None' | 'Cash' | 'Bank';

export type LimitPolicy = 'Allow' | 'Warn' | 'Block';

export type DocumentStatus = 'Active' | 'Cancelled';

export type DocumentStatusFilter = 'Active' | 'Cancelled' | 'All';

export type DebtSourceType = 'Opening' | 'StockIn' | 'StockOut' | 'Receipt' | 'Payment' | 'Offset';

export type SettlementOrigin = 'AtVoucher' | 'Manual' | 'Offset' | 'Auto';

export type OpeningDebtKind = 'Debt' | 'Prepayment';

// The four Round 2A document types (a subset of the backend DocumentType).
export type CashDocumentType = 'Receipt' | 'Payment' | 'FundTransfer' | 'DebtOffset';

export type CashDocumentActivityAction =
  | 'Created'
  | 'Updated'
  | 'Cancelled'
  | 'Restored'
  | 'Deleted'
  | 'SettlementRemoved';

export interface CashDocumentActivity {
  id: string;
  action: CashDocumentActivityAction;
  actorUserId?: string;
  actorName?: string;
  occurredAt: string;
  description: string;
}
