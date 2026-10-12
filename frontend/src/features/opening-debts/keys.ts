import { cashKeys } from '@/features/cash-common/keys';
import type { OpeningDebtListParams } from './types';

// Branch-scoped, so under the ['cash'] root (P28).
export const openingDebtKeys = {
  all: [...cashKeys.all, 'opening-debts'] as const,
  list: (params: OpeningDebtListParams) => [...openingDebtKeys.all, 'list', params] as const,
  partners: (keyword: string) => [...openingDebtKeys.all, 'partners', keyword] as const,
};
