import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import type { DebtSide } from '@/features/cash-common/types';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { debtApi, partnerSummaryApi } from './api';
import { debtKeys, partnerSummaryKey } from './keys';
import type { CreateSettlementBatchRequest, DebtPartnerSearchItem, OpenItemsParams, SettlementListParams } from './types';

type PartnerSearchKey = ReturnType<typeof debtKeys.partners>;

// An empty keyword returns the first partners, so a picker can list them on focus. No side: any role;
// bothRoles: only partners that are both customer and supplier. The previous results are kept while the
// keyword changes, never across another side or role filter (they could hold partners of the wrong role).
export function useDebtPartnerSearch(
  side: DebtSide | undefined,
  keyword: string,
  { bothRoles = false }: { bothRoles?: boolean } = {},
) {
  const trimmed = keyword.trim();
  const queryKey = debtKeys.partners(side, trimmed, bothRoles);
  return useQuery({
    queryKey,
    queryFn: () =>
      debtApi.searchPartners({ side, keyword: trimmed || undefined, bothRoles: bothRoles || undefined }),
    staleTime: 30_000,
    placeholderData: (previous: DebtPartnerSearchItem[] | undefined, previousQuery) =>
      previousQuery && sameRoleFilter(previousQuery.queryKey, queryKey) ? previous : undefined,
  });
}

// Every part but the last (the keyword) must match.
function sameRoleFilter(previous: readonly unknown[], next: PartnerSearchKey): boolean {
  return previous.length === next.length && next.slice(0, -1).every((part, index) => previous[index] === part);
}

// No placeholder: items of another partner or side must never show while the new ones load.
export function useOpenItems(params: OpenItemsParams, opts: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: debtKeys.openItems(params),
    queryFn: () => debtApi.openItems(params),
    enabled: (opts.enabled ?? true) && !!params.partnerId,
  });
}

// Needs debt.matching. No placeholder, for the same reason as the open items.
export function useDebtSettlements(params: SettlementListParams, opts: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: debtKeys.settlements(params),
    queryFn: () => debtApi.settlements(params),
    enabled: (opts.enabled ?? true) && !!params.partnerId,
  });
}

// Needs debt.partner_summary or reports.debt; callers gate `enabled` on the permission.
export function usePartnerDebtSummary(partnerId: string | undefined, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: partnerSummaryKey(partnerId ?? ''),
    queryFn: () => partnerSummaryApi.get(partnerId!),
    enabled: enabled && !!partnerId,
  });
}

// Settlements change open items, balances and reports under ['cash'], and stock voucher lists show
// settlement-driven state (P28): refresh both roots.
function useInvalidateDebt() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: cashKeys.all });
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
  };
}

export function useCreateSettlementBatch() {
  const invalidate = useInvalidateDebt();
  return useMutation({
    mutationFn: (data: CreateSettlementBatchRequest) => debtApi.createSettlementBatch(data),
    onSuccess: invalidate,
  });
}

export function useDeleteSettlement() {
  const invalidate = useInvalidateDebt();
  return useMutation({
    mutationFn: (id: string) => debtApi.deleteSettlement(id),
    onSuccess: invalidate,
  });
}

export function useDeleteSettlementBatch() {
  const invalidate = useInvalidateDebt();
  return useMutation({
    mutationFn: (batchId: string) => debtApi.deleteSettlementBatch(batchId),
    onSuccess: invalidate,
  });
}
