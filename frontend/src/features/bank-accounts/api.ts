import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type { CreateUserBankAccountRequest, UpdateUserBankAccountRequest, UserBankAccount } from './types';

export const bankAccountsApi = {
  list: () => apiGet<UserBankAccount[]>('/me/bank-accounts'),
  create: (data: CreateUserBankAccountRequest) => apiPost<UserBankAccount>('/me/bank-accounts', data),
  update: (id: string, data: UpdateUserBankAccountRequest) =>
    apiPut<UserBankAccount>(`/me/bank-accounts/${id}`, data),
  remove: (id: string) => apiDelete<void>(`/me/bank-accounts/${id}`),
  setDefault: (id: string) => apiPut<UserBankAccount>(`/me/bank-accounts/${id}/default`),
};
