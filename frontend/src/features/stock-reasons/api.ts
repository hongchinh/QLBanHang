import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  CreateStockReasonRequest,
  StockReason,
  StockReasonListParams,
  UpdateStockReasonRequest,
} from './types';

export const stockReasonsApi = {
  list: (params?: StockReasonListParams) => apiGet<StockReason[]>('/stock-reasons', params),
  get: (id: string) => apiGet<StockReason>(`/stock-reasons/${id}`),
  create: (data: CreateStockReasonRequest) => apiPost<StockReason>('/stock-reasons', data),
  update: (id: string, data: UpdateStockReasonRequest) =>
    apiPut<StockReason>(`/stock-reasons/${id}`, data),
  remove: (id: string) => apiDelete(`/stock-reasons/${id}`),
};
