import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import { fromDateTimeLocalValue } from '@/lib/vn-datetime';
import type {
  PartnerSearchItem,
  StockAtRequest,
  StockAtResult,
  StockDirection,
  StockVoucher,
  StockVoucherActionRequest,
  StockVoucherActivity,
  StockVoucherDefaults,
  StockVoucherListParams,
  StockVoucherListResult,
  StockVoucherOwner,
  UpsertStockVoucherRequest,
} from './types';

export const stockVouchersApi = {
  list: (params: StockVoucherListParams) => {
    const { status, ownerUserIds, ...rest } = params;
    const serialized: Record<string, unknown> = { ...rest };
    if (status && status !== 'all') serialized.status = status;
    if (ownerUserIds && ownerUserIds.length > 0) serialized.ownerUserIds = ownerUserIds.join(',');
    return apiGet<StockVoucherListResult>('/stock-vouchers', serialized);
  },
  owners: (type: StockDirection) => apiGet<StockVoucherOwner[]>('/stock-vouchers/owners', { type }),
  // voucherAt is a datetime-local value; the backend needs an instant with an offset.
  defaults: (type: StockDirection, voucherAt?: string) =>
    apiGet<StockVoucherDefaults>('/stock-vouchers/defaults', {
      type,
      voucherAt: voucherAt ? fromDateTimeLocalValue(voucherAt) : undefined,
    }),
  stockAt: (body: StockAtRequest) => apiPost<StockAtResult[]>('/stock-vouchers/stock-at', body),
  partners: (type: StockDirection, keyword: string, reasonId?: string) =>
    apiGet<PartnerSearchItem[]>('/stock-vouchers/partners', { type, keyword, reasonId }),
  get: (id: string) => apiGet<StockVoucher>(`/stock-vouchers/${id}`),
  activities: (id: string) => apiGet<StockVoucherActivity[]>(`/stock-vouchers/${id}/activities`),
  create: (data: UpsertStockVoucherRequest) => apiPost<StockVoucher>('/stock-vouchers', data),
  update: (id: string, data: UpsertStockVoucherRequest) =>
    apiPut<StockVoucher>(`/stock-vouchers/${id}`, data),
  cancel: (id: string, body: StockVoucherActionRequest) =>
    apiPost<StockVoucher>(`/stock-vouchers/${id}/cancel`, body),
  restore: (id: string, body: StockVoucherActionRequest) =>
    apiPost<StockVoucher>(`/stock-vouchers/${id}/restore`, body),
  // DELETE carries version and acknowledgeNegativeStock in the query string.
  remove: (id: string, body: StockVoucherActionRequest) =>
    apiDelete(`/stock-vouchers/${id}`, { params: body }),
};
