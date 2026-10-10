import { cashKeys } from '@/features/cash-common/keys';
import type { DebtSide } from '@/features/cash-common/types';

export const debtKeys = {
  all: [...cashKeys.all, 'debt'] as const,
  partners: (side: DebtSide, keyword: string) => [...debtKeys.all, 'partners', side, keyword] as const,
};
