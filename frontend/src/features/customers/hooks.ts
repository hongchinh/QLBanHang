import {
  useMutation,
  useQuery,
  useQueryClient,
  keepPreviousData,
  type QueryClient,
} from '@tanstack/react-query';
import { customersApi } from './api';
import { cashVoucherKeys } from '@/features/cash-vouchers/keys';
import { stockVoucherKeys } from '@/features/stock-vouchers/keys';
import { supplierKeys } from '@/features/suppliers/keys';
import { customerKeys } from './keys';
import type { CustomerListParams, UpsertCustomerRequest } from './types';

// A dual-role partner shows up in both catalogs, so customer and supplier
// mutations refresh both, plus the cash and stock voucher partner searches.
export function invalidatePartnerQueries(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: customerKeys.all });
  qc.invalidateQueries({ queryKey: supplierKeys.all });
  qc.invalidateQueries({ queryKey: cashVoucherKeys.partnersAll() });
  qc.invalidateQueries({ queryKey: stockVoucherKeys.partnersAll() });
}

export function useCustomers(params: CustomerListParams) {
  return useQuery({
    queryKey: customerKeys.list(params),
    queryFn: () => customersApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useCustomer(id: string | undefined) {
  return useQuery({
    queryKey: customerKeys.detail(id ?? ''),
    queryFn: () => customersApi.get(id!),
    enabled: !!id,
  });
}

export function useCustomersSearch(
  keyword: string,
  opts?: { activeOnly?: boolean; limit?: number },
) {
  const trimmed = keyword.trim();
  const params = { keyword: trimmed, activeOnly: opts?.activeOnly ?? true, limit: opts?.limit ?? 20 };
  return useQuery({
    queryKey: customerKeys.search(params),
    queryFn: () => customersApi.search(params),
    enabled: trimmed.length > 0,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });
}

export function useCreateCustomer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpsertCustomerRequest) => customersApi.create(data),
    onSuccess: () => invalidatePartnerQueries(qc),
  });
}

export function useUpdateCustomer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertCustomerRequest }) =>
      customersApi.update(id, data),
    onSuccess: () => invalidatePartnerQueries(qc),
  });
}

export function useDeleteCustomer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => customersApi.remove(id),
    onSuccess: () => invalidatePartnerQueries(qc),
  });
}
