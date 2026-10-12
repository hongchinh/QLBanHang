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
  // DELETE binds version, confirmRemoveSettlements and confirmedSettlements from the query string.
  remove: (id: string, version: number, confirmRemoveSettlements: boolean, confirmedSettlements?: string) =>
    apiDelete(`/opening-debts/${id}`, { params: { version, confirmRemoveSettlements, confirmedSettlements } }),
  // GET /api/debt/partners without a side: partners of every role, inactive ones included (list filter).
  searchPartners: (keyword: string) =>
    apiGet<DebtPartnerSearchItem[]>('/debt/partners', { keyword, activeOnly: false }),
};
