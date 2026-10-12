import { apiDelete, apiGet, apiPost } from '@/lib/api-client';
import type {
  CreateDebtOffsetRequest,
  DebtOffset,
  DebtOffsetActionRequest,
  DebtOffsetActivity,
  DebtOffsetDefaults,
  DebtOffsetListParams,
  DebtOffsetListResult,
} from './types';

// No update and no restore endpoint (P23).
export const debtOffsetsApi = {
  list: (params: DebtOffsetListParams) => apiGet<DebtOffsetListResult>('/debt-offsets', params),
  defaults: () => apiGet<DebtOffsetDefaults>('/debt-offsets/defaults'),
  get: (id: string) => apiGet<DebtOffset>(`/debt-offsets/${id}`),
  activities: (id: string) => apiGet<DebtOffsetActivity[]>(`/debt-offsets/${id}/activities`),
  create: (data: CreateDebtOffsetRequest) => apiPost<DebtOffset>('/debt-offsets', data),
  cancel: (id: string, body: DebtOffsetActionRequest) => apiPost<DebtOffset>(`/debt-offsets/${id}/cancel`, body),
  // DELETE carries version, confirmRemoveSettlements and confirmedSettlements in the query string.
  remove: (id: string, body: DebtOffsetActionRequest) => apiDelete(`/debt-offsets/${id}`, { params: body }),
};
