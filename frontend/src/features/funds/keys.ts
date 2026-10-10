import { cashKeys } from '@/features/cash-common/keys';
import type { FundListParams } from './types';

// Funds are branch-scoped, so they live under the cash root (P28).
export const fundKeys = {
  all: [...cashKeys.all, 'funds'] as const,
  lists: () => [...fundKeys.all, 'list'] as const,
  list: (params?: FundListParams) => [...fundKeys.lists(), params ?? {}] as const,
};
