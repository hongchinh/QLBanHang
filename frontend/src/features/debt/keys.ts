import { cashKeys } from '@/features/cash-common/keys';
import type { DebtSide } from '@/features/cash-common/types';
import type { OpenItemsParams } from './types';

export const debtKeys = {
  all: [...cashKeys.all, 'debt'] as const,
  partners: (side: DebtSide, keyword: string) => [...debtKeys.all, 'partners', side, keyword] as const,
  openItems: (params: OpenItemsParams) => [...debtKeys.all, 'open-items', params] as const,
};

export const partnerSummaryKey = (partnerId: string) => [...debtKeys.all, 'partner-summary', partnerId] as const;
