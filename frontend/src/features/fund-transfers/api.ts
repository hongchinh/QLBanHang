import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  FundTransfer,
  FundTransferActionRequest,
  FundTransferActivity,
  FundTransferDefaults,
  FundTransferListParams,
  FundTransferListResult,
  UpsertFundTransferRequest,
} from './types';

export const fundTransfersApi = {
  list: (params: FundTransferListParams) => apiGet<FundTransferListResult>('/fund-transfers', params),
  defaults: () => apiGet<FundTransferDefaults>('/fund-transfers/defaults'),
  get: (id: string) => apiGet<FundTransfer>(`/fund-transfers/${id}`),
  activities: (id: string) => apiGet<FundTransferActivity[]>(`/fund-transfers/${id}/activities`),
  create: (data: UpsertFundTransferRequest) => apiPost<FundTransfer>('/fund-transfers', data),
  update: (id: string, data: UpsertFundTransferRequest) => apiPut<FundTransfer>(`/fund-transfers/${id}`, data),
  cancel: (id: string, body: FundTransferActionRequest) =>
    apiPost<FundTransfer>(`/fund-transfers/${id}/cancel`, body),
  restore: (id: string, body: FundTransferActionRequest) =>
    apiPost<FundTransfer>(`/fund-transfers/${id}/restore`, body),
  // DELETE carries version and acknowledgeNegativeFund in the query string.
  remove: (id: string, body: FundTransferActionRequest) => apiDelete(`/fund-transfers/${id}`, { params: body }),
};
