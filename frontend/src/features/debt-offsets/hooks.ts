import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { debtOffsetsApi } from './api';
import { debtOffsetKeys } from './keys';
import type { CreateDebtOffsetRequest, DebtOffsetActionRequest, DebtOffsetListParams } from './types';

export function useDebtOffsets(params: DebtOffsetListParams) {
  return useQuery({
    queryKey: debtOffsetKeys.list(params),
    queryFn: () => debtOffsetsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useDebtOffset(id: string | undefined) {
  return useQuery({
    queryKey: debtOffsetKeys.detail(id ?? ''),
    queryFn: () => debtOffsetsApi.get(id!),
    enabled: !!id,
  });
}

export function useDebtOffsetActivities(id: string | undefined) {
  return useQuery({
    queryKey: debtOffsetKeys.activities(id ?? ''),
    queryFn: () => debtOffsetsApi.activities(id!),
    enabled: !!id,
  });
}

// Never served from cache: a new offset must start at the real server "now" and next number.
export function useDebtOffsetDefaults(opts: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: debtOffsetKeys.defaults(),
    queryFn: () => debtOffsetsApi.defaults(),
    enabled: opts.enabled ?? true,
    staleTime: 0,
    gcTime: 0,
    refetchOnMount: 'always',
  });
}

// An offset settles documents: open items, balances and reports under ['cash'] change, and stock voucher lists
// show settlement-driven state (P28), so both roots are refreshed.
function useInvalidateDebt() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: cashKeys.all });
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
  };
}

export function useCreateDebtOffset() {
  const invalidate = useInvalidateDebt();
  return useMutation({
    mutationFn: (data: CreateDebtOffsetRequest) => debtOffsetsApi.create(data),
    onSuccess: invalidate,
  });
}

export function useCancelDebtOffset() {
  const qc = useQueryClient();
  const invalidate = useInvalidateDebt();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: DebtOffsetActionRequest }) => debtOffsetsApi.cancel(id, body),
    onSuccess: (saved) => {
      // The view shows the cancelled offset at once; the invalidation then refreshes the rest.
      qc.setQueryData(debtOffsetKeys.detail(saved.id), saved);
      invalidate();
    },
  });
}

export function useDeleteDebtOffset() {
  const qc = useQueryClient();
  const invalidate = useInvalidateDebt();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: DebtOffsetActionRequest }) => debtOffsetsApi.remove(id, body),
    onSuccess: (_data, { id }) => {
      // Drop the deleted offset first so the invalidation does not refetch it into a 404.
      qc.removeQueries({ queryKey: debtOffsetKeys.detail(id) });
      invalidate();
    },
  });
}
