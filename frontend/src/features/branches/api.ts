import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  Branch,
  CreateBranchRequest,
  DemoBranchDeletionPreview,
  DemoBranchDeletionResult,
  MyBranches,
  UpdateBranchRequest,
} from './types';

export const branchesApi = {
  list: () => apiGet<Branch[]>('/branches'),
  get: (id: string) => apiGet<Branch>(`/branches/${id}`),
  create: (data: CreateBranchRequest) => apiPost<Branch>('/branches', data),
  update: (id: string, data: UpdateBranchRequest) => apiPut<Branch>(`/branches/${id}`, data),
  remove: (id: string) => apiDelete(`/branches/${id}`),
  setLock: (id: string, lockedUntil: string | null) =>
    apiPut<Branch>(`/branches/${id}/lock`, { lockedUntil }),
  setOpeningDate: (id: string, openingDate: string | null) =>
    apiPut<Branch>(`/branches/${id}/opening-date`, { openingDate }),
  deletionPreview: (id: string) =>
    apiGet<DemoBranchDeletionPreview>(`/branches/${id}/demo-deletion`),
  removeDemo: (id: string) => apiDelete<DemoBranchDeletionResult>(`/branches/${id}/demo`),
  me: () => apiGet<MyBranches>('/me/branches'),
};
