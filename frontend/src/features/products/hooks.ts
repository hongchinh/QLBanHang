import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { lookupsApi, productsApi } from './api';
import { lookupKeys, productKeys } from './keys';
import type { CreateProductRequest, ProductListParams, UpdateProductRequest } from './types';

export function useProducts(params: ProductListParams) {
  return useQuery({
    queryKey: productKeys.list(params),
    queryFn: () => productsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useProduct(id: string | undefined) {
  return useQuery({
    queryKey: productKeys.detail(id ?? ''),
    queryFn: () => productsApi.get(id!),
    enabled: !!id,
  });
}

export function useCreateProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProductRequest) => productsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: productKeys.lists() }),
  });
}

export function useUpdateProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateProductRequest }) =>
      productsApi.update(id, data),
    onSuccess: (_data, { id }) => {
      qc.invalidateQueries({ queryKey: productKeys.lists() });
      qc.invalidateQueries({ queryKey: productKeys.detail(id) });
    },
  });
}

export function useDeleteProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => productsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: productKeys.lists() }),
  });
}

export function useProductGroups() {
  return useQuery({
    queryKey: lookupKeys.productGroups(),
    queryFn: () => lookupsApi.productGroups(),
    staleTime: 5 * 60 * 1000,
  });
}

export function useUnits() {
  return useQuery({
    queryKey: lookupKeys.units(),
    queryFn: () => lookupsApi.units(),
    staleTime: 5 * 60 * 1000,
  });
}

export function useProductSearch(query: string) {
  const debounced = useDebouncedValue(query, 200);
  const result = useQuery({
    queryKey: productKeys.search(debounced),
    queryFn: () => productsApi.search(debounced),
    enabled: debounced.trim().length >= 1,
    staleTime: 30_000,
  });
  // The query the results belong to: lets the cell tell results of older text from results of the text shown.
  return { ...result, settledQuery: debounced };
}
