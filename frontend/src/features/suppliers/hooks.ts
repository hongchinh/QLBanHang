import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { invalidatePartnerQueries } from '@/features/customers/hooks';
import type { CustomerListParams, UpsertCustomerRequest } from '@/features/customers/types';
import { suppliersApi } from './api';
import { supplierKeys } from './keys';

export function useSuppliers(params: CustomerListParams) {
  return useQuery({
    queryKey: supplierKeys.list(params),
    queryFn: () => suppliersApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useSupplier(id: string | undefined) {
  return useQuery({
    queryKey: supplierKeys.detail(id ?? ''),
    queryFn: () => suppliersApi.get(id!),
    enabled: !!id,
  });
}

export function useCreateSupplier() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpsertCustomerRequest) => suppliersApi.create(data),
    onSuccess: () => invalidatePartnerQueries(qc),
  });
}

export function useUpdateSupplier() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertCustomerRequest }) =>
      suppliersApi.update(id, data),
    onSuccess: () => invalidatePartnerQueries(qc),
  });
}

export function useDeleteSupplier() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => suppliersApi.remove(id),
    onSuccess: () => invalidatePartnerQueries(qc),
  });
}
