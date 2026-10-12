import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  CashDocumentActionRequest,
  CashDocumentActivity,
  CashPartnerSearchItem,
  CashVoucher,
  CashVoucherDefaults,
  CashVoucherListParams,
  CashVoucherListResult,
  CashVoucherType,
  UpsertCashVoucherRequest,
} from './types';

export const cashVouchersApi = {
  list: (params: CashVoucherListParams) => {
    const { ownerIds, ...rest } = params;
    const serialized: Record<string, unknown> = { ...rest };
    if (ownerIds && ownerIds.length > 0) serialized.ownerIds = ownerIds.join(',');
    return apiGet<CashVoucherListResult>('/cash-vouchers', serialized);
  },
  defaults: (type: CashVoucherType) => apiGet<CashVoucherDefaults>('/cash-vouchers/defaults', { type }),
  partners: (type: CashVoucherType, keyword: string, reasonId?: string) =>
    apiGet<CashPartnerSearchItem[]>('/cash-vouchers/partners', { type, keyword, reasonId }),
  get: (id: string) => apiGet<CashVoucher>(`/cash-vouchers/${id}`),
  activities: (id: string) => apiGet<CashDocumentActivity[]>(`/cash-vouchers/${id}/activities`),
  create: (data: UpsertCashVoucherRequest) => apiPost<CashVoucher>('/cash-vouchers', data),
  update: (id: string, data: UpsertCashVoucherRequest) => apiPut<CashVoucher>(`/cash-vouchers/${id}`, data),
  cancel: (id: string, body: CashDocumentActionRequest) =>
    apiPost<CashVoucher>(`/cash-vouchers/${id}/cancel`, body),
  restore: (id: string, body: CashDocumentActionRequest) =>
    apiPost<CashVoucher>(`/cash-vouchers/${id}/restore`, body),
  // DELETE carries version and the confirmation flags in the query string.
  remove: (id: string, body: CashDocumentActionRequest) => apiDelete(`/cash-vouchers/${id}`, { params: body }),
};
