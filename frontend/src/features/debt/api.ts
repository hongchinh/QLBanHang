import { apiDelete, apiGet, apiPost } from '@/lib/api-client';
import type {
  CreateSettlementBatchRequest,
  DebtPartnerSearchItem,
  DebtPartnerSearchParams,
  DebtSettlement,
  OpenItem,
  OpenItemsParams,
  PartnerDebtSummary,
  SettlementBatch,
  SettlementListParams,
} from './types';

export const debtApi = {
  searchPartners: (params: DebtPartnerSearchParams) =>
    apiGet<DebtPartnerSearchItem[]>('/debt/partners', params),
  openItems: (params: OpenItemsParams) => apiGet<OpenItem[]>('/debt/open-items', params),
  settlements: (params: SettlementListParams) => apiGet<DebtSettlement[]>('/debt/settlements', params),
  createSettlementBatch: (data: CreateSettlementBatchRequest) =>
    apiPost<SettlementBatch>('/debt/settlements', data),
  // Auto and Offset settlements belong to their documents: the server answers 409 SETTLEMENT_MANAGED (P22).
  deleteSettlement: (id: string) => apiDelete(`/debt/settlements/${id}`),
  deleteSettlementBatch: (batchId: string) => apiDelete(`/debt/settlement-batches/${batchId}`),
};

export const partnerSummaryApi = {
  get: (partnerId: string) => apiGet<PartnerDebtSummary>('/debt/partner-summary', { partnerId }),
};
