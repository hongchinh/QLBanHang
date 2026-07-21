import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { bankAccountsApi } from './api';
import type { CreateUserBankAccountRequest, UpdateUserBankAccountRequest } from './types';

const KEYS = { list: ['bank-accounts'] as const };

export function useMyBankAccounts() {
  return useQuery({ queryKey: KEYS.list, queryFn: bankAccountsApi.list });
}

export function useCreateBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateUserBankAccountRequest) => bankAccountsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}

export function useUpdateBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserBankAccountRequest }) =>
      bankAccountsApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}

export function useDeleteBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => bankAccountsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}

export function useSetDefaultBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => bankAccountsApi.setDefault(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}
