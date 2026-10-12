import { cashKeys } from '@/features/cash-common/keys';
import type { CreditLimitListParams } from './types';

// Branch-scoped, so under the ['cash'] root (P28).
export const creditLimitKeys = {
  all: [...cashKeys.all, 'credit-limits'] as const,
  list: (params: CreditLimitListParams) => [...creditLimitKeys.all, 'list', params] as const,
};
