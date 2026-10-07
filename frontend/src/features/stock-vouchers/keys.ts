import type { StockAtRequest, StockDirection, StockVoucherListParams } from './types';

// Shared inventory root: opening stock and reports (Phase 10) and inventory settings nest under it,
// so a voucher change invalidates every inventory figure at once.
export const inventoryKeys = {
  all: ['inventory'] as const,
};

export const stockVoucherKeys = {
  all: [...inventoryKeys.all, 'stock-vouchers'] as const,
  lists: () => [...stockVoucherKeys.all, 'list'] as const,
  list: (params: StockVoucherListParams) => [...stockVoucherKeys.lists(), params] as const,
  details: () => [...stockVoucherKeys.all, 'detail'] as const,
  detail: (id: string) => [...stockVoucherKeys.details(), id] as const,
  activities: (id: string) => [...stockVoucherKeys.detail(id), 'activities'] as const,
  owners: (type: StockDirection) => [...stockVoucherKeys.all, 'owners', type] as const,
  defaults: (type: StockDirection, voucherAt?: string) =>
    [...stockVoucherKeys.all, 'defaults', type, voucherAt ?? null] as const,
  stockAt: (body: StockAtRequest) => [...stockVoucherKeys.all, 'stock-at', body] as const,
  partners: (type: StockDirection, keyword: string, reasonId?: string) =>
    [...stockVoucherKeys.all, 'partners', { type, keyword, reasonId: reasonId ?? null }] as const,
};
