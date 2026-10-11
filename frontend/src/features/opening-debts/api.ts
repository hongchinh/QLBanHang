import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import type {
  OpeningDebt,
  OpeningDebtListParams,
  OpeningDebtListResult,
  UpsertOpeningDebtRequest,
} from './types';

export const openingDebtsApi = {
  list: (params: OpeningDebtListParams) => apiGet<OpeningDebtListResult>('/opening-debts', params),
  create: (data: UpsertOpeningDebtRequest) => apiPost<OpeningDebt>('/opening-debts', data),
  update: (id: string, data: UpsertOpeningDebtRequest) => apiPut<OpeningDebt>(`/opening-debts/${id}`, data),
  // DELETE binds confirmRemoveSettlements from the query string.
  remove: (id: string, confirmRemoveSettlements: boolean) =>
    apiDelete(`/opening-debts/${id}`, { params: { confirmRemoveSettlements } }),
  // GET /api/debt/partners without a side: partners of every role, inactive ones included (list filter).
  searchPartners: (keyword: string) =>
    apiGet<DebtPartnerSearchItem[]>('/debt/partners', { keyword, activeOnly: false }),
};
