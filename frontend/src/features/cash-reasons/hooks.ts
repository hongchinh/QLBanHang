import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashReasonsApi } from './api';
import { cashReasonKeys } from './keys';
import type {
  CashReasonListParams,
  CreateCashReasonRequest,
  UpdateCashReasonRequest,
} from './types';

export function useCashReasons(params?: CashReasonListParams) {
  return useQuery({
    queryKey: cashReasonKeys.list(params),
    queryFn: () => cashReasonsApi.list(params),
  });
}

export function useCreateCashReason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateCashReasonRequest) => cashReasonsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashReasonKeys.all }),
  });
}

export function useUpdateCashReason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateCashReasonRequest }) =>
      cashReasonsApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashReasonKeys.all }),
  });
}

export function useDeleteCashReason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cashReasonsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashReasonKeys.all }),
  });
}
