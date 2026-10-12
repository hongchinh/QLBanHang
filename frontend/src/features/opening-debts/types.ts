import type { DebtSide, OpeningDebtKind } from '@/features/cash-common/types';
import type { PagedResult } from '@/features/customers/types';

// One opening debt row of the working branch (OpeningDebtDto).
export interface OpeningDebt {
  id: string;
  partnerId: string;
  partnerCode: string;
  partnerName: string;
  side: DebtSide;
  kind: OpeningDebtKind;
  docNo: string;
  // yyyy-MM-dd (VN dates)
  docDate: string;
  dueDate?: string | null;
  amount: number;
  // Σ settlements of the row's debt entry.
  settled: number;
  ownerUserId?: string | null;
  ownerName?: string | null;
  note?: string | null;
  // xmin row version (D29): sent back on update and delete.
  version: number;
}

// GET /api/opening-debts/owners: the active users who can own an opening debt (B1).
export interface OpeningDebtOwner {
  id: string;
  fullName: string;
}

// GET /api/opening-debts: the rows plus the branch opening date (null while it is not set).
export interface OpeningDebtListResult extends PagedResult<OpeningDebt> {
  openingDate?: string | null;
}

export interface OpeningDebtListParams {
  partnerId?: string;
  side?: DebtSide;
  search?: string;
  page: number;
  pageSize: number;
}

// POST /api/opening-debts and PUT /api/opening-debts/{id} (UpsertOpeningDebtRequest).
export interface UpsertOpeningDebtRequest {
  partnerId: string;
  side: DebtSide;
  kind: OpeningDebtKind;
  docNo: string;
  docDate: string;
  // Always null for a prepayment.
  dueDate: string | null;
  amount: number;
  // The salesperson who owns the debt (B1); null clears it.
  ownerUserId?: string | null;
  note?: string;
  confirmRemoveSettlements: boolean;
  confirmedSettlements?: string;
  // Required on update, omitted on create.
  version?: number;
}
