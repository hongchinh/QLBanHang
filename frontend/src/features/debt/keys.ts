import { cashKeys } from '@/features/cash-common/keys';
import type { DebtSide } from '@/features/cash-common/types';
import type { OpenItemsParams, SettlementListParams } from './types';

export const debtKeys = {
  all: [...cashKeys.all, 'debt'] as const,
  // The keyword stays last: the partner search keeps previous results only while the role filter is the same.
  partners: (side: DebtSide | undefined, keyword: string, bothRoles = false) =>
    [...debtKeys.all, 'partners', side ?? null, bothRoles, keyword] as const,
  openItems: (params: OpenItemsParams) => [...debtKeys.all, 'open-items', params] as const,
  settlements: (params: SettlementListParams) => [...debtKeys.all, 'settlements', params] as const,
};

export const partnerSummaryKey = (partnerId: string) => [...debtKeys.all, 'partner-summary', partnerId] as const;
