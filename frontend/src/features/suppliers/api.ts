import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  Customer,
  CustomerListItem,
  CustomerListParams,
  CustomerSearchItem,
  CustomerSearchParams,
  PagedResult,
  UpsertCustomerRequest,
} from '@/features/customers/types';

// Suppliers share the partner catalog (and its types) with customers; the backend
// filters IsSupplier and checks suppliers.* permissions (D1, D38).
export const suppliersApi = {
  list: (params: CustomerListParams) =>
    apiGet<PagedResult<CustomerListItem>>('/suppliers', params),
  search: (params: CustomerSearchParams) =>
    apiGet<CustomerSearchItem[]>('/suppliers/search', {
      keyword: params.keyword,
      activeOnly: params.activeOnly ?? true,
      limit: params.limit ?? 20,
    }),
  get: (id: string) => apiGet<Customer>(`/suppliers/${id}`),
  create: (data: UpsertCustomerRequest) => apiPost<Customer>('/suppliers', data),
  update: (id: string, data: UpsertCustomerRequest) => apiPut<Customer>(`/suppliers/${id}`, data),
  remove: (id: string) => apiDelete(`/suppliers/${id}`),
};
