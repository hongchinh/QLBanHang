import { apiDelete, apiGet, apiPut } from '@/lib/api-client';
import type {
  CreditLimit,
  CreditLimitListParams,
  CreditLimitListResult,
  UpsertCreditLimitRequest,
} from './types';

export const creditLimitsApi = {
  list: (params: CreditLimitListParams) => apiGet<CreditLimitListResult>('/credit-limits', params),
  upsert: (customerId: string, data: UpsertCreditLimitRequest) =>
    apiPut<CreditLimit>(`/credit-limits/${customerId}`, data),
  remove: (customerId: string) => apiDelete(`/credit-limits/${customerId}`),
};
