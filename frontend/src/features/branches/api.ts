import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type { Branch, CreateBranchRequest, MyBranches, UpdateBranchRequest } from './types';

export const branchesApi = {
  list: () => apiGet<Branch[]>('/branches'),
  get: (id: string) => apiGet<Branch>(`/branches/${id}`),
  create: (data: CreateBranchRequest) => apiPost<Branch>('/branches', data),
  update: (id: string, data: UpdateBranchRequest) => apiPut<Branch>(`/branches/${id}`, data),
  remove: (id: string) => apiDelete(`/branches/${id}`),
  setLock: (id: string, lockedUntil: string | null) =>
    apiPut<Branch>(`/branches/${id}/lock`, { lockedUntil }),
  me: () => apiGet<MyBranches>('/me/branches'),
};
