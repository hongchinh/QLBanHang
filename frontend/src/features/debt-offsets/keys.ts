import { cashKeys } from '@/features/cash-common/keys';
import type { DebtOffsetListParams } from './types';

// Debt offsets are branch-scoped, so they live under the cash root (P28).
export const debtOffsetKeys = {
  all: [...cashKeys.all, 'debt-offsets'] as const,
  lists: () => [...debtOffsetKeys.all, 'list'] as const,
  list: (params: DebtOffsetListParams) => [...debtOffsetKeys.lists(), params] as const,
  details: () => [...debtOffsetKeys.all, 'detail'] as const,
  detail: (id: string) => [...debtOffsetKeys.details(), id] as const,
  activities: (id: string) => [...debtOffsetKeys.detail(id), 'activities'] as const,
  defaults: () => [...debtOffsetKeys.all, 'defaults'] as const,
};
