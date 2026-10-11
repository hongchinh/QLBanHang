import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { DebtSide } from '@/features/cash-common/types';
import { debtApi } from './api';
import { debtKeys } from './keys';
import type { OpenItemsParams } from './types';

// An empty keyword returns the first partners of the side, so a picker can list them on focus.
// The side is part of the key; callers pass a fixed side, so the previous results stay of that side.
export function useDebtPartnerSearch(side: DebtSide, keyword: string) {
  const trimmed = keyword.trim();
  return useQuery({
    queryKey: debtKeys.partners(side, trimmed),
    queryFn: () => debtApi.searchPartners({ side, keyword: trimmed || undefined }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });
}

// No placeholder: items of another partner or side must never show while the new ones load.
export function useOpenItems(params: OpenItemsParams, opts: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: debtKeys.openItems(params),
    queryFn: () => debtApi.openItems(params),
    enabled: (opts.enabled ?? true) && !!params.partnerId,
  });
}
