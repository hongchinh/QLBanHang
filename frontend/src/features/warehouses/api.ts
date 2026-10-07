import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  CreateWarehouseRequest,
  UpdateWarehouseRequest,
  Warehouse,
  WarehouseListParams,
} from './types';

export const warehousesApi = {
  list: (params?: WarehouseListParams) => apiGet<Warehouse[]>('/warehouses', params),
  get: (id: string) => apiGet<Warehouse>(`/warehouses/${id}`),
  create: (data: CreateWarehouseRequest) => apiPost<Warehouse>('/warehouses', data),
  update: (id: string, data: UpdateWarehouseRequest) =>
    apiPut<Warehouse>(`/warehouses/${id}`, data),
  remove: (id: string) => apiDelete(`/warehouses/${id}`),
};
