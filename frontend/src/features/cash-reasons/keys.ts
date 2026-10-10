import type { CashReasonListParams } from './types';

// Cash reasons are shared by every branch, so they keep their own root (not ['cash']).
export const cashReasonKeys = {
  all: ['cash-reasons'] as const,
  lists: () => [...cashReasonKeys.all, 'list'] as const,
  list: (params?: CashReasonListParams) => [...cashReasonKeys.lists(), params ?? {}] as const,
};
