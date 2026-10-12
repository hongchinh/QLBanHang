import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { cashVouchersApi } from './api';
import { cashVoucherKeys } from './keys';
import type {
  CashDocumentActionRequest,
  CashPartnerSearchItem,
  CashVoucherListParams,
  CashVoucherType,
  UpsertCashVoucherRequest,
} from './types';

export function useCashVouchers(params: CashVoucherListParams) {
  return useQuery({
    queryKey: cashVoucherKeys.list(params),
    queryFn: () => cashVouchersApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useCashVoucher(id: string | undefined) {
  return useQuery({
    queryKey: cashVoucherKeys.detail(id ?? ''),
    queryFn: () => cashVouchersApi.get(id!),
    enabled: !!id,
  });
}

export function useCashVoucherActivities(id: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: cashVoucherKeys.activities(id ?? ''),
    queryFn: () => cashVouchersApi.activities(id!),
    enabled: !!id && enabled,
  });
}

// `fresh` never serves cached data: a new voucher must start at the real "now" and the next code.
export function useCashVoucherDefaults(
  type: CashVoucherType,
  opts: { enabled?: boolean; fresh?: boolean } = {},
) {
  return useQuery({
    queryKey: cashVoucherKeys.defaults(type),
    queryFn: () => cashVouchersApi.defaults(type),
    enabled: opts.enabled ?? true,
    ...(opts.fresh ? { staleTime: 0, gcTime: 0, refetchOnMount: 'always' as const } : {}),
  });
}

export function useCashPartnerSearch(type: CashVoucherType, keyword: string, reasonId?: string) {
  const trimmed = keyword.trim();
  const queryKey = cashVoucherKeys.partners(type, trimmed, reasonId);
  return useQuery({
    queryKey,
    queryFn: () => cashVouchersApi.partners(type, trimmed, reasonId),
    enabled: trimmed.length > 0,
    staleTime: 30_000,
    // Keep the previous results only while the role filter is the same: results of another reason
    // may hold partners of the wrong role and must not be selectable while the new search loads.
    placeholderData: (previous: CashPartnerSearchItem[] | undefined, previousQuery) =>
      previousQuery && sameRoleFilter(previousQuery.queryKey, queryKey) ? previous : undefined,
  });
}

type CashPartnerSearchKey = ReturnType<typeof cashVoucherKeys.partners>;

// The last key part is { type, keyword, reasonId }; only the keyword may differ.
function sameRoleFilter(previous: readonly unknown[], next: CashPartnerSearchKey): boolean {
  const before = previous[previous.length - 1] as CashPartnerSearchKey[3] | undefined;
  const after = next[3];
  return before?.type === after.type && before?.reasonId === after.reasonId;
}

// A cash voucher moves fund balances and debt settlements, and stock voucher lists show
// settlement-driven state (P28): refresh both roots.
function useInvalidateCash() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: cashKeys.all });
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
  };
}

export function useCreateCashVoucher() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: (data: UpsertCashVoucherRequest) => cashVouchersApi.create(data),
    onSuccess: invalidate,
  });
}

export function useUpdateCashVoucher() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertCashVoucherRequest }) =>
      cashVouchersApi.update(id, data),
    onSuccess: invalidate,
  });
}

export function useCancelCashVoucher() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: CashDocumentActionRequest }) =>
      cashVouchersApi.cancel(id, body),
    onSuccess: invalidate,
  });
}

export function useRestoreCashVoucher() {
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: CashDocumentActionRequest }) =>
      cashVouchersApi.restore(id, body),
    onSuccess: invalidate,
  });
}

export function useDeleteCashVoucher() {
  const qc = useQueryClient();
  const invalidate = useInvalidateCash();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: CashDocumentActionRequest }) =>
      cashVouchersApi.remove(id, body),
    onSuccess: (_data, { id }) => {
      // Drop the deleted voucher first so the invalidation does not refetch it into a 404.
      qc.removeQueries({ queryKey: cashVoucherKeys.detail(id) });
      invalidate();
    },
  });
}
