import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { branchesApi } from './api';
import { branchKeys } from './keys';
import type { CreateBranchRequest, UpdateBranchRequest } from './types';

function invalidateBranches(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: branchKeys.lists() });
  qc.invalidateQueries({ queryKey: branchKeys.me() });
}

export function useBranches() {
  return useQuery({
    queryKey: branchKeys.lists(),
    queryFn: () => branchesApi.list(),
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

export function useSetPeriodLock() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, lockedUntil }: { id: string; lockedUntil: string | null }) =>
      branchesApi.setLock(id, lockedUntil),
    onSuccess: () => invalidateBranches(qc),
  });
}
