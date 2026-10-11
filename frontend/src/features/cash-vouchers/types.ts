import type {
  CashDocumentActivity,
  CashVoucherType,
  DebtEffect,
  DebtSourceType,
  DocumentStatus,
  DocumentStatusFilter,
  FundKind,
  SettlementOrigin,
} from '@/features/cash-common/types';
import type { PagedResult } from '@/features/customers/types';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import type { ConfirmFlags } from '@/lib/confirm-required';

// Mirrors backend Application/CashDebt/CashVouchers/Models/CashVoucherDtos.cs.

export interface CashVoucherLine {
  id: string;
  sortOrder: number;
  description: string;
  amount: number;
  note?: string;
}

export interface CashVoucherAllocation {
  settlementId: string;
  entryId: string;
  sourceType: DebtSourceType;
  sourceId: string;
  docCode: string;
  // yyyy-MM-dd (VN date)
  docDate: string;
  amount: number;
  origin: SettlementOrigin;
  // An AtVoucher settlement this voucher made. Another voucher's AtVoucher settlement against it is not edited here.
  isOwn: boolean;
}

export interface CashVoucher {
  id: string;
  type: CashVoucherType;
  code: string;
  voucherAt: string;
  branchId: string;
  fundId: string;
  fundCode: string;
  fundName: string;
  fundKind: FundKind;
  reasonId: string;
  reasonCode: string;
  reasonName: string;
  reasonDebtEffect: DebtEffect;
  partnerId?: string;
  partnerCode?: string;
  partnerName?: string;
  partnerAddress?: string;
  partnerTaxCode?: string;
  personName?: string;
  description?: string;
  attachedDocs?: string;
  note?: string;
  total: number;
  status: DocumentStatus;
  cancelledAt?: string;
  ownerUserId: string;
  ownerName?: string;
  // PostgreSQL xmin; sent back on update/cancel/restore/delete.
  version: number;
  // Automatic vouchers (P20) are managed by their stock voucher and read-only here.
  sourceStockVoucherId?: string;
  sourceStockVoucherCode?: string;
  sourceStockVoucherType?: StockDirection;
  isManaged: boolean;
  canEdit: boolean;
  canCancel: boolean;
  canDelete: boolean;
  lines: CashVoucherLine[];
  // Every settlement of the voucher's own debt entry; only the AtVoucher ones are edited on the form.
  allocations: CashVoucherAllocation[];
}

export interface CashVoucherListItem {
  id: string;
  type: CashVoucherType;
  code: string;
  voucherAt: string;
  fundCode: string;
  fundName: string;
  reasonName: string;
  partnerCode?: string;
  partnerName?: string;
  personName?: string;
  description?: string;
  total: number;
  status: DocumentStatus;
  ownerUserId: string;
  ownerName?: string;
  sourceStockVoucherId?: string;
  sourceStockVoucherCode?: string;
  isManaged: boolean;
}

export interface CashVoucherListResult extends PagedResult<CashVoucherListItem> {
  // Σ Total of the whole filtered set (status All includes cancelled vouchers).
  totalAmount: number;
}

export interface CashVoucherListParams {
  type: CashVoucherType;
  page?: number;
  pageSize?: number;
  search?: string;
  // yyyy-MM-dd (VN dates)
  from?: string;
  to?: string;
  fundId?: string;
  reasonId?: string;
  partnerId?: string;
  // Omitted → the server default (Active).
  status?: DocumentStatusFilter;
  ownerIds?: string[];
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CashVoucherDefaults {
  // Peek only: the counter is not used.
  nextCode: string;
  voucherAt: string;
  // Default cash fund of the working branch.
  fundId?: string | null;
  // TKH (receipt) or TNCC (payment).
  reasonId?: string | null;
}

export interface UpsertCashVoucherLineRequest {
  id?: string;
  description: string;
  amount: number;
  note?: string;
}

export interface CashAllocationRequest {
  entryId: string;
  amount: number;
}

export interface UpsertCashVoucherRequest
  extends Pick<ConfirmFlags, 'confirmRemoveSettlements' | 'acknowledgeNegativeFund'> {
  // Create: chooses the type; update: must equal the stored type.
  type: CashVoucherType;
  // ISO instant
  voucherAt: string;
  fundId: string;
  reasonId: string;
  partnerId?: string;
  // Omitted → the partner's catalog values.
  partnerName?: string;
  partnerAddress?: string;
  partnerTaxCode?: string;
  personName?: string;
  description?: string;
  attachedDocs?: string;
  note?: string;
  // Required on update.
  version?: number;
  lines: UpsertCashVoucherLineRequest[];
  // The AtVoucher settlements; omitted on update keeps the stored ones, [] removes them.
  allocations?: CashAllocationRequest[];
}

export interface CashDocumentActionRequest
  extends Pick<ConfirmFlags, 'confirmRemoveSettlements' | 'acknowledgeNegativeFund'> {
  version?: number;
}

// GET /cash-vouchers/partners returns the customer search shape with the partner roles.
export type CashPartnerSearchItem = DebtPartnerSearchItem;

export type { CashDocumentActivity, CashVoucherType };
