import type { CustomerSearchItem } from '@/features/customers/types';
import type { DebtDirection, DebtSide, DebtSourceType, SettlementOrigin } from '@/features/cash-common/types';

// GET /api/debt/partners (CustomerSearchItemDto): Receivable → customers, Payable → suppliers.
export interface DebtPartnerSearchItem extends CustomerSearchItem {
  isCustomer: boolean;
  isSupplier: boolean;
  creditDays?: number | null;
}

// No side: any role; bothRoles: only partners that are both customer and supplier (debt offsets).
export interface DebtPartnerSearchParams {
  side?: DebtSide;
  bothRoles?: boolean;
  keyword?: string;
  limit?: number;
  // The server default is true (active partners only).
  activeOnly?: boolean;
}

// GET /api/debt/open-items (OpenItemDto): open entries of one partner and side in the working branch.
export interface OpenItem {
  entryId: string;
  sourceType: DebtSourceType;
  sourceId: string;
  docCode: string;
  // yyyy-MM-dd (VN dates)
  docDate: string;
  dueDate?: string | null;
  postedAt: string;
  direction: DebtDirection;
  amount: number;
  settled: number;
  open: number;
  description?: string;
  ownerUserId?: string;
}

// forSourceType/forSourceId (P26): the open amount adds back the settlements with that source's own entry,
// so an edit grid shows the room the voucher already uses.
export interface OpenItemsParams {
  partnerId: string;
  side: DebtSide;
  direction?: DebtDirection;
  forSourceType?: DebtSourceType;
  forSourceId?: string;
}

// GET /api/debt/partner-summary (P6): the partner's receivable and payable position in the working branch.
export interface PartnerDebtSummary {
  partnerId: string;
  receivableBalance: number;
  // Σ Increase − Σ Decrease of the payable side.
  payableBalance: number;
  // null: no credit limit for the partner in the branch.
  creditLimit?: number | null;
  // creditLimit − receivableBalance, may be negative; null without a limit.
  available?: number | null;
  overdueAmount: number;
  creditDays?: number | null;
}

// GET /api/debt/settlements (needs debt.matching): every settlement between entries of one partner and side
// in the working branch.
export interface SettlementListParams {
  partnerId: string;
  side: DebtSide;
}

// SettlementDto: a settlement with the code, date and source of both its entries.
export interface DebtSettlement {
  id: string;
  batchId: string;
  origin: SettlementOrigin;
  increaseEntryId: string;
  increaseDocCode: string;
  // yyyy-MM-dd (VN date)
  increaseDocDate: string;
  increaseSourceType: DebtSourceType;
  increaseSourceId: string;
  decreaseEntryId: string;
  decreaseDocCode: string;
  // yyyy-MM-dd (VN date)
  decreaseDocDate: string;
  decreaseSourceType: DebtSourceType;
  decreaseSourceId: string;
  amount: number;
  effectiveAt: string;
  createdAt: string;
  createdByName?: string | null;
}

export interface SettlementItemRequest {
  increaseEntryId: string;
  decreaseEntryId: string;
  amount: number;
}

// POST /api/debt/settlements: one Manual batch (P22).
export interface CreateSettlementBatchRequest {
  partnerId: string;
  side: DebtSide;
  items: SettlementItemRequest[];
}

export interface SettlementBatch {
  batchId: string;
  settlements: DebtSettlement[];
}
