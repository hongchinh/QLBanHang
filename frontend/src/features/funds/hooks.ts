import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { fundsApi } from './api';
import { fundKeys } from './keys';
import type {
  CreateFundRequest,
  FundListParams,
  SetFundOpeningBalanceRequest,
  UpdateFundRequest,
} from './types';

export function useFunds(params?: FundListParams) {
  return useQuery({
    queryKey: fundKeys.list(params),
    queryFn: () => fundsApi.list(params),
  });
}

export function useCreateFund() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFundRequest) => fundsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: fundKeys.all }),
  });
}

export function useUpdateFund() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateFundRequest }) => fundsApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: fundKeys.all }),
  });
}

export function useDeleteFund() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => fundsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: fundKeys.all }),
  });
}

// The opening amount shifts every running balance of the fund (cash book), so the whole cash root is dropped.
export function useSetFundOpeningBalance() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: SetFundOpeningBalanceRequest }) =>
      fundsApi.setOpeningBalance(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashKeys.all }),
  });
}
