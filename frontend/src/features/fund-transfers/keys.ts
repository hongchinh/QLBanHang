import { cashKeys } from '@/features/cash-common/keys';
import type { FundTransferListParams } from './types';

// Fund transfers are branch-scoped, so they live under the cash root (P28).
export const fundTransferKeys = {
  all: [...cashKeys.all, 'fund-transfers'] as const,
  lists: () => [...fundTransferKeys.all, 'list'] as const,
  list: (params: FundTransferListParams) => [...fundTransferKeys.lists(), params] as const,
  details: () => [...fundTransferKeys.all, 'detail'] as const,
  detail: (id: string) => [...fundTransferKeys.details(), id] as const,
  activities: (id: string) => [...fundTransferKeys.detail(id), 'activities'] as const,
  defaults: () => [...fundTransferKeys.all, 'defaults'] as const,
};
