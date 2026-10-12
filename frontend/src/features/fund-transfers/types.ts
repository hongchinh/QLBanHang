import type { PagedResult } from '@/features/customers/types';
import type {
  CashDocumentActivity,
  CashDocumentActivityAction,
  DocumentStatus,
  DocumentStatusFilter,
  FundKind,
} from '@/features/cash-common/types';

// Mirrors backend Application/CashDebt/FundTransfers/Models/FundTransferDtos.cs.

export interface FundTransfer {
  id: string;
  code: string;
  voucherAt: string;
  branchId: string;
  fromFundId: string;
  fromFundCode: string;
  fromFundName: string;
  fromFundKind: FundKind;
  toFundId: string;
  toFundCode: string;
  toFundName: string;
  toFundKind: FundKind;
  amount: number;
  description?: string | null;
  note?: string | null;
  status: DocumentStatus;
  cancelledAt?: string | null;
  ownerUserId: string;
  ownerName?: string | null;
  version: number;
  canEdit: boolean;
  // Also true for a cancelled transfer: restore uses the cancel permission.
  canCancel: boolean;
  canDelete: boolean;
}

export interface FundTransferListItem {
  id: string;
  code: string;
  voucherAt: string;
  fromFundCode: string;
  fromFundName: string;
  toFundCode: string;
  toFundName: string;
  amount: number;
  description?: string | null;
  status: DocumentStatus;
  ownerUserId: string;
  ownerName?: string | null;
}

// totalAmount = Σ amount of the whole filtered set (cancelled transfers included when status = All).
export interface FundTransferListResult extends PagedResult<FundTransferListItem> {
  totalAmount: number;
}

export interface FundTransferListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  // yyyy-MM-dd (VN dates)
  from?: string;
  to?: string;
  // Matches the source or the destination fund.
  fundId?: string;
  status?: DocumentStatusFilter;
}

export interface FundTransferDefaults {
  nextCode: string;
  voucherAt: string;
}

export interface UpsertFundTransferRequest {
  voucherAt: string;
  fromFundId: string;
  toFundId: string;
  amount: number;
  description?: string;
  note?: string;
  version?: number;
  acknowledgeNegativeFund: boolean;
}

export interface FundTransferActionRequest {
  version?: number;
  acknowledgeNegativeFund: boolean;
}

// A transfer has no debt, so its activities never carry SettlementRemoved.
export type FundTransferActivity = Omit<CashDocumentActivity, 'action'> & {
  action: Exclude<CashDocumentActivityAction, 'SettlementRemoved'>;
};
