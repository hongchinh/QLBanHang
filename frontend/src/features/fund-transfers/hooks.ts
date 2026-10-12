import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { fundTransfersApi } from './api';
import { fundTransferKeys } from './keys';
import type { FundTransferActionRequest, FundTransferListParams, UpsertFundTransferRequest } from './types';

export function useFundTransfers(params: FundTransferListParams) {
  return useQuery({
    queryKey: fundTransferKeys.list(params),
    queryFn: () => fundTransfersApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useFundTransfer(id: string | undefined) {
  return useQuery({
    queryKey: fundTransferKeys.detail(id ?? ''),
    queryFn: () => fundTransfersApi.get(id!),
    enabled: !!id,
  });
}

export function useFundTransferActivities(id: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: fundTransferKeys.activities(id ?? ''),
    queryFn: () => fundTransfersApi.activities(id!),
    enabled: !!id && enabled,
  });
}

// Never served from cache: a new transfer must start at the real server "now" and next number.
export function useFundTransferDefaults(opts: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: fundTransferKeys.defaults(),
    queryFn: () => fundTransfersApi.defaults(),
    enabled: opts.enabled ?? true,
    staleTime: 0,
    gcTime: 0,
    refetchOnMount: 'always',
  });
}

// Same as the other cash mutations: refresh the whole cash root (fund balances, cash book) and the inventory root.
function useInvalidateCash() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: cashKeys.all });
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
  };
}

export function useCreateFundTransfer() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: (data: UpsertFundTransferRequest) => fundTransfersApi.create(data),
    onSuccess: invalidate,
  });
}

export function useUpdateFundTransfer() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertFundTransferRequest }) => fundTransfersApi.update(id, data),
    onSuccess: invalidate,
  });
}

export function useCancelFundTransfer() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: FundTransferActionRequest }) => fundTransfersApi.cancel(id, body),
    onSuccess: invalidate,
  });
}

export function useRestoreFundTransfer() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: FundTransferActionRequest }) => fundTransfersApi.restore(id, body),
    onSuccess: invalidate,
  });
}

export function useDeleteFundTransfer() {
  const qc = useQueryClient();
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: FundTransferActionRequest }) => fundTransfersApi.remove(id, body),
    onSuccess: (_data, { id }) => {
      // Drop the deleted transfer first so the invalidation does not refetch it into a 404.
      qc.removeQueries({ queryKey: fundTransferKeys.detail(id) });
      invalidate();
    },
  });
}
