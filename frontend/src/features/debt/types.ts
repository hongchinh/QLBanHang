import type { CustomerSearchItem } from '@/features/customers/types';
import type { DebtDirection, DebtSide, DebtSourceType } from '@/features/cash-common/types';

// GET /api/debt/partners (CustomerSearchItemDto): Receivable → customers, Payable → suppliers.
export interface DebtPartnerSearchItem extends CustomerSearchItem {
  isCustomer: boolean;
  isSupplier: boolean;
  creditDays?: number | null;
}

export interface DebtPartnerSearchParams {
  side?: DebtSide;
  keyword?: string;
  limit?: number;
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
