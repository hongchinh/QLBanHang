import { cashKeys } from '@/features/cash-common/keys';
import type { CashVoucherListParams, CashVoucherType } from './types';

export const cashVoucherKeys = {
  all: [...cashKeys.all, 'cash-vouchers'] as const,
  lists: () => [...cashVoucherKeys.all, 'list'] as const,
  list: (params: CashVoucherListParams) => [...cashVoucherKeys.lists(), params] as const,
  details: () => [...cashVoucherKeys.all, 'detail'] as const,
  detail: (id: string) => [...cashVoucherKeys.details(), id] as const,
  activities: (id: string) => [...cashVoucherKeys.detail(id), 'activities'] as const,
  defaults: (type: CashVoucherType) => [...cashVoucherKeys.all, 'defaults', type] as const,
  partnersAll: () => [...cashVoucherKeys.all, 'partners'] as const,
  partners: (type: CashVoucherType, keyword: string, reasonId?: string, activeOnly?: boolean) =>
    [
      ...cashVoucherKeys.all,
      'partners',
      { type, keyword, reasonId: reasonId ?? null, activeOnly: activeOnly ?? null },
    ] as const,
};
