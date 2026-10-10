import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { creditLimitsApi } from './api';
import type { CreditLimitListParams } from './types';

// Branch-scoped, so under the ['cash'] root (P28).
export const creditLimitKeys = {
  all: [...cashKeys.all, 'credit-limits'] as const,
  list: (params: CreditLimitListParams) => [...creditLimitKeys.all, 'list', params] as const,
};

export function useCreditLimits(params: CreditLimitListParams) {
  return useQuery({
    queryKey: creditLimitKeys.list(params),
    queryFn: () => creditLimitsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

// A limit also feeds the partner debt summary, so a change refreshes every ['cash'] query.
export function useUpsertCreditLimit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ customerId, amount }: { customerId: string; amount: number }) =>
      creditLimitsApi.upsert(customerId, { amount }),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashKeys.all }),
  });
}

export function useDeleteCreditLimit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (customerId: string) => creditLimitsApi.remove(customerId),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashKeys.all }),
  });
}
