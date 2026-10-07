import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { stockReasonsApi } from './api';
import { stockReasonKeys } from './keys';
import type {
  CreateStockReasonRequest,
  StockReasonListParams,
  UpdateStockReasonRequest,
} from './types';

export function useStockReasons(params?: StockReasonListParams) {
  return useQuery({
    queryKey: stockReasonKeys.list(params),
    queryFn: () => stockReasonsApi.list(params),
  });
}

export function useCreateStockReason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateStockReasonRequest) => stockReasonsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: stockReasonKeys.all }),
  });
}

export function useUpdateStockReason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateStockReasonRequest }) =>
      stockReasonsApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: stockReasonKeys.all }),
  });
}

export function useDeleteStockReason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => stockReasonsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: stockReasonKeys.all }),
  });
}
