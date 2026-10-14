import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { branchesApi } from './api';
import { branchKeys } from './keys';
import { branchesRefetchInterval } from './sample-data';
import type { CreateBranchRequest, UpdateBranchRequest } from './types';

function invalidateBranches(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: branchKeys.lists() });
  qc.invalidateQueries({ queryKey: branchKeys.me() });
}

export function useBranches() {
  return useQuery({
    queryKey: branchKeys.lists(),
    queryFn: () => branchesApi.list(),
    // Poll while a demo branch's sample data is being filled.
    refetchInterval: (query) => branchesRefetchInterval(query.state.data),
    refetchIntervalInBackground: false,
  });
}

export function useMyBranches() {
  return useQuery({
    queryKey: branchKeys.me(),
    queryFn: () => branchesApi.me(),
  });
}

export function useCreateBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateBranchRequest) => branchesApi.create(data),
    onSuccess: () => invalidateBranches(qc),
  });
}

export function useUpdateBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateBranchRequest }) =>
      branchesApi.update(id, data),
    onSuccess: () => invalidateBranches(qc),
  });
}

export function useDeleteBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => branchesApi.remove(id),
    onSuccess: () => invalidateBranches(qc),
  });
}

export function useDemoBranchDeletionPreview(id: string | null) {
  return useQuery({
    queryKey: branchKeys.demoDeletion(id),
    queryFn: () => branchesApi.deletionPreview(id!),
    enabled: !!id,
    staleTime: 0,
  });
}

export function useDeleteDemoBranch() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => branchesApi.removeDemo(id),
    onSuccess: () => invalidateBranches(qc),
  });
}

export function useSetPeriodLock() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, lockedUntil }: { id: string; lockedUntil: string | null }) =>
      branchesApi.setLock(id, lockedUntil),
    onSuccess: () => invalidateBranches(qc),
  });
}

// The change re-posts the branch's opening stock and moves its opening debt (P8),
// so the inventory and cash roots are refetched too.
export function useSetBranchOpeningDate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, openingDate }: { id: string; openingDate: string | null }) =>
      branchesApi.setOpeningDate(id, openingDate),
    onSuccess: () => {
      invalidateBranches(qc);
      qc.invalidateQueries({ queryKey: inventoryKeys.all });
      qc.invalidateQueries({ queryKey: cashKeys.all });
    },
  });
}
