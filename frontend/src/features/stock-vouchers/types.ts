import type { CashVoucherType, DebtEffect } from '@/features/cash-common/types';
import type { CustomerSearchItem, PagedResult } from '@/features/customers/types';
import type { PricingMode } from '@/features/products/types';
import type { StockDirection } from '@/features/stock-reasons/types';

export type StockVoucherStatus = 'Active' | 'Cancelled';
// 'all' is a real filter value; api.ts maps it to "no status param".
export type StockVoucherStatusFilter = StockVoucherStatus | 'all';
export type StockVoucherActivityAction =
  | 'Created'
  | 'Updated'
  | 'Cancelled'
  | 'Restored'
  | 'Deleted'
  | 'SettlementRemoved'
  | 'AutoVoucherChanged';

export interface StockVoucherLine {
  id: string;
  sortOrder: number;
  productId: string;
  productCode: string;
  productName: string;
  warehouseId: string;
  warehouseCode?: string;
  trackInventory: boolean;
  pricingMode: PricingMode;
  unitName: string;
  priceIncludesVat: boolean;
  sheetCount?: number;
  length?: number;
  width?: number;
  thickness?: number;
  quantity: number;
  unitPrice: number;
  amount: number;
  discountRate: number;
  discountAmount: number;
  discountManual: boolean;
  orderDiscountAllocated: number;
  freightAllocated: number;
  vatRate: number;
  vatAmount: number;
  netAmount: number;
  note?: string;
}

export interface StockVoucher {
  id: string;
  type: StockDirection;
  code: string;
  voucherAt: string;
  branchId: string;
  warehouseId: string;
  warehouseCode?: string;
  warehouseName?: string;
  partnerId?: string;
  partnerCode?: string;
  partnerName?: string;
  partnerAddress?: string;
  partnerTaxCode?: string;
  handlerName?: string;
  reasonId: string;
  reasonName?: string;
  reasonDebtEffect: DebtEffect;
  paymentMethodId?: string;
  paymentMethodName?: string;
  // yyyy-MM-dd
  dueDate?: string | null;
  fundId?: string | null;
  fundCode?: string | null;
  fundName?: string | null;
  // The automatic receipt (stock-out) or payment (stock-in) of the paid amount (P20).
  autoCashVoucherId?: string | null;
  autoCashVoucherCode?: string | null;
  autoCashVoucherType?: CashVoucherType | null;
  note?: string;
  freight: number;
  orderDiscount: number;
  goodsAmount: number;
  lineDiscountTotal: number;
  discountTotal: number;
  vatTotal: number;
  total: number;
  paidAmount: number;
  status: StockVoucherStatus;
  cancelledAt?: string;
  cancelledBy?: string;
  ownerUserId: string;
  ownerName?: string;
  createdAt: string;
  // PostgreSQL xmin (D16); sent back on update/cancel/restore/delete.
  version: number;
  canEdit: boolean;
  canCancel: boolean;
  canDelete: boolean;
  lines: StockVoucherLine[];
}

export interface StockVoucherListItem {
  id: string;
  type: StockDirection;
  code: string;
  voucherAt: string;
  warehouseName?: string;
  partnerName?: string;
  reasonName?: string;
  paymentMethodName?: string;
  goodsAmount: number;
  discountTotal: number;
  vatTotal: number;
  freight: number;
  total: number;
  paidAmount: number;
  status: StockVoucherStatus;
  ownerUserId: string;
  ownerName?: string;
  createdAt: string;
}

// The backend excludes cancelled vouchers from the aggregates.
export interface StockVoucherListAggregates {
  goodsAmount: number;
  discountTotal: number;
  vatTotal: number;
  freight: number;
  total: number;
  paidAmount: number;
}

export interface StockVoucherListResult extends PagedResult<StockVoucherListItem> {
  aggregates: StockVoucherListAggregates;
}

export interface StockVoucherListParams {
  type: StockDirection;
  page?: number;
  pageSize?: number;
  search?: string;
  // yyyy-MM-dd (VN dates)
  from?: string;
  to?: string;
  warehouseId?: string;
  partnerId?: string;
  reasonId?: string;
  status?: StockVoucherStatusFilter;
  ownerUserIds?: string[];
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface StockVoucherOwner {
  id: string;
  fullName: string;
}

export interface StockVoucherActivity {
  id: string;
  action: StockVoucherActivityAction;
  actorUserId?: string;
  actorName?: string;
  occurredAt: string;
  description: string;
}

export interface StockVoucherDefaults {
  nextCode: string;
  voucherAt: string;
  warehouseId?: string;
  reasonId?: string;
  paymentMethodId?: string;
}

export interface StockAtItem {
  productId: string;
  warehouseId: string;
}

export interface StockAtRequest {
  type: StockDirection;
  // ISO instant
  at: string;
  excludeVoucherId?: string;
  items: StockAtItem[];
}

export interface StockAtResult {
  productId: string;
  warehouseId: string;
  quantity: number;
}

export interface UpsertStockVoucherLineRequest {
  id?: string;
  sortOrder: number;
  productId: string;
  // Omitted → header warehouse.
  warehouseId?: string;
  sheetCount?: number;
  length?: number;
  width?: number;
  thickness?: number;
  // PerUnit only; the backend computes the others.
  quantity: number;
  unitPrice: number;
  discountRate: number;
  discountAmount?: number;
  discountManual: boolean;
  vatRate: number;
  note?: string;
}

export interface UpsertStockVoucherRequest {
  type: StockDirection;
  // ISO instant
  voucherAt: string;
  warehouseId: string;
  reasonId: string;
  partnerId?: string;
  // Omitted → the partner's catalog values.
  partnerName?: string;
  partnerAddress?: string;
  partnerTaxCode?: string;
  handlerName?: string;
  paymentMethodId?: string;
  note?: string;
  freight: number;
  orderDiscount: number;
  // Omitted → Total when the payment method has a fund kind, else 0 (P9).
  paidAmount?: number;
  // yyyy-MM-dd; omitted → VN date + partner credit days on debt-increasing reasons (P27).
  dueDate?: string;
  // Omitted → the branch's default fund of the payment method's kind.
  fundId?: string;
  // Required on update (D16).
  version?: number;
  acknowledgeNegativeStock: boolean;
  confirmRemoveSettlements?: boolean;
  acknowledgeCreditLimit?: boolean;
  acknowledgeNegativeFund?: boolean;
  lines: UpsertStockVoucherLineRequest[];
}

export interface StockVoucherActionRequest {
  version?: number;
  acknowledgeNegativeStock?: boolean;
  confirmRemoveSettlements?: boolean;
  acknowledgeCreditLimit?: boolean;
  acknowledgeNegativeFund?: boolean;
}

// GET /stock-vouchers/partners returns the customer search shape with the partner roles.
export interface PartnerSearchItem extends CustomerSearchItem {
  isCustomer: boolean;
  isSupplier: boolean;
  creditDays?: number | null;
}

export type { StockDirection };
