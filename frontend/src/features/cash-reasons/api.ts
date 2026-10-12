import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  CashReason,
  CashReasonListParams,
  CreateCashReasonRequest,
  UpdateCashReasonRequest,
} from './types';

export const cashReasonsApi = {
  list: (params?: CashReasonListParams) => apiGet<CashReason[]>('/cash-reasons', params),
  get: (id: string) => apiGet<CashReason>(`/cash-reasons/${id}`),
  create: (data: CreateCashReasonRequest) => apiPost<CashReason>('/cash-reasons', data),
  update: (id: string, data: UpdateCashReasonRequest) =>
    apiPut<CashReason>(`/cash-reasons/${id}`, data),
  remove: (id: string) => apiDelete(`/cash-reasons/${id}`),
};
