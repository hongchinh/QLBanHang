import { apiGet } from '@/lib/api-client';
import type { DebtPartnerSearchItem, DebtPartnerSearchParams, OpenItem, OpenItemsParams } from './types';

export const debtApi = {
  searchPartners: (params: DebtPartnerSearchParams) =>
    apiGet<DebtPartnerSearchItem[]>('/debt/partners', params),
  openItems: (params: OpenItemsParams) => apiGet<OpenItem[]>('/debt/open-items', params),
};
