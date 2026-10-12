import type { PagedResult } from '@/features/customers/types';
import type {
  CashDocumentActivity,
  CashDocumentActivityAction,
  DebtSide,
  DebtSourceType,
  DocumentStatus,
  DocumentStatusFilter,
} from '@/features/cash-common/types';

// Mirrors backend Application/CashDebt/DebtOffsets/Models/DebtOffsetDtos.cs.

// One offset document: the entry settled against the offset's own entry of that side.
export interface DebtOffsetItem {
  side: DebtSide;
  entryId: string;
  sourceType: DebtSourceType;
  sourceId: string;
  docCode: string;
  // yyyy-MM-dd (VN date)
  docDate: string;
  amount: number;
}

export interface DebtOffset {
  id: string;
  code: string;
  voucherAt: string;
  partnerId: string;
  partnerCode?: string | null;
  partnerName: string;
  // Server amount: it can be larger than Σ items once a document it offset was cancelled or shrunk.
  amount: number;
  note?: string | null;
  status: DocumentStatus;
  cancelledAt?: string | null;
  ownerUserId: string;
  ownerName?: string | null;
  version: number;
  canCancel: boolean;
  canDelete: boolean;
  // The documents it offsets, receivables first. Empty once cancelled (P23).
  items: DebtOffsetItem[];
}

export interface DebtOffsetListItem {
  id: string;
  code: string;
  voucherAt: string;
  partnerCode?: string | null;
  partnerName: string;
  amount: number;
  note?: string | null;
  status: DocumentStatus;
  ownerName?: string | null;
}

// totalAmount = Σ amount of the whole filtered set, not only the page.
export interface DebtOffsetListResult extends PagedResult<DebtOffsetListItem> {
  totalAmount: number;
}

export interface DebtOffsetListParams {
  page?: number;
  pageSize?: number;
  // Code, partner name or note.
  search?: string;
  // yyyy-MM-dd (VN dates)
  from?: string;
  to?: string;
  partnerId?: string;
  status?: DocumentStatusFilter;
}

export interface DebtOffsetDefaults {
  // Peek only: the counter is not used.
  nextCode: string;
  voucherAt: string;
}

export interface DebtOffsetAllocation {
  entryId: string;
  amount: number;
}

// Each row is an open Increase entry of the partner on that side; Σ receivables = Σ payables > 0.
export interface CreateDebtOffsetRequest {
  voucherAt: string;
  partnerId: string;
  note?: string;
  receivables: DebtOffsetAllocation[];
  payables: DebtOffsetAllocation[];
}

// Cancel (body) and delete (query string). The flag answers DEBT_SETTLEMENTS_WILL_BE_REMOVED (P10).
export interface DebtOffsetActionRequest {
  version?: number;
  confirmRemoveSettlements: boolean;
  confirmedSettlements?: string;
}

// No edit and no restore (P23).
export type DebtOffsetActivity = Omit<CashDocumentActivity, 'action'> & {
  action: Exclude<CashDocumentActivityAction, 'Updated' | 'Restored'>;
};
