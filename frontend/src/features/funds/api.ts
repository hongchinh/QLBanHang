import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  CreateFundRequest,
  Fund,
  FundListParams,
  SetFundOpeningBalanceRequest,
  UpdateFundRequest,
} from './types';

export const fundsApi = {
  list: (params?: FundListParams) => apiGet<Fund[]>('/funds', params),
  get: (id: string) => apiGet<Fund>(`/funds/${id}`),
  create: (data: CreateFundRequest) => apiPost<Fund>('/funds', data),
  update: (id: string, data: UpdateFundRequest) => apiPut<Fund>(`/funds/${id}`, data),
  setOpeningBalance: (id: string, data: SetFundOpeningBalanceRequest) =>
    apiPut<Fund>(`/funds/${id}/opening-balance`, data),
  remove: (id: string) => apiDelete(`/funds/${id}`),
};
