import { apiGet } from '@/lib/api-client';
import type { DebtPartnerSearchItem, DebtPartnerSearchParams } from './types';

export const debtApi = {
  searchPartners: (params: DebtPartnerSearchParams) =>
    apiGet<DebtPartnerSearchItem[]>('/debt/partners', params),
};
