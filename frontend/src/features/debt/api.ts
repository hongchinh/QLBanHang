import { apiGet } from '@/lib/api-client';
import type {
  DebtPartnerSearchItem,
  DebtPartnerSearchParams,
  OpenItem,
  OpenItemsParams,
  PartnerDebtSummary,
} from './types';

export const debtApi = {
  searchPartners: (params: DebtPartnerSearchParams) =>
    apiGet<DebtPartnerSearchItem[]>('/debt/partners', params),
  openItems: (params: OpenItemsParams) => apiGet<OpenItem[]>('/debt/open-items', params),
};

export const partnerSummaryApi = {
  get: (partnerId: string) => apiGet<PartnerDebtSummary>('/debt/partner-summary', { partnerId }),
};
