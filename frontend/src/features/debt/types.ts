import type { CustomerSearchItem } from '@/features/customers/types';
import type { DebtSide } from '@/features/cash-common/types';

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
