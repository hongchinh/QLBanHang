import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { productKeys } from '@/features/products/keys';
import { stockVouchersApi } from './api';
import { inventoryKeys, stockVoucherKeys } from './keys';
import type {
  PartnerSearchItem,
  StockAtRequest,
  StockDirection,
  StockVoucher,
  StockVoucherActionRequest,
  StockVoucherListParams,
  UpsertStockVoucherRequest,
} from './types';

export function useStockVouchers(params: StockVoucherListParams) {
  return useQuery({
    queryKey: stockVoucherKeys.list(params),
    queryFn: () => stockVouchersApi.list(params),
    placeholderData: keepPreviousData,
  });
}

export function useStockVoucher(id: string | undefined) {
  return useQuery({
    queryKey: stockVoucherKeys.detail(id ?? ''),
    queryFn: () => stockVouchersApi.get(id!),
    enabled: !!id,
  });
}

export function useStockVoucherActivities(id: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: stockVoucherKeys.activities(id ?? ''),
    queryFn: () => stockVouchersApi.activities(id!),
    enabled: !!id && enabled,
  });
}

export function useStockVoucherOwners(type: StockDirection) {
  return useQuery({
    queryKey: stockVoucherKeys.owners(type),
    queryFn: () => stockVouchersApi.owners(type),
    staleTime: 5 * 60_000,
  });
}

// voucherAt is a datetime-local value; omitted → server "now".
// `fresh` never serves cached data: a new voucher must start at the real "now", not a cached one.
export function useStockVoucherDefaults(
  type: StockDirection,
  voucherAt?: string,
  opts: { enabled?: boolean; fresh?: boolean } = {},
) {
  return useQuery({
    queryKey: stockVoucherKeys.defaults(type, voucherAt),
    queryFn: () => stockVouchersApi.defaults(type, voucherAt),
    enabled: opts.enabled ?? true,
    ...(opts.fresh
      ? { staleTime: 0, gcTime: 0, refetchOnMount: 'always' as const }
      : { placeholderData: keepPreviousData }),
  });
}

export function useStockAt(body: StockAtRequest) {
  return useQuery({
    queryKey: stockVoucherKeys.stockAt(body),
    queryFn: () => stockVouchersApi.stockAt(body),
    enabled: body.items.length > 0,
    placeholderData: keepPreviousData,
  });
}

export function usePartnerSearch(type: StockDirection, keyword: string, reasonId?: string) {
  const trimmed = keyword.trim();
  const queryKey = stockVoucherKeys.partners(type, trimmed, reasonId);
  return useQuery({
    queryKey,
    queryFn: () => stockVouchersApi.partners(type, trimmed, reasonId),
    enabled: trimmed.length > 0,
    staleTime: 30_000,
    // Keep the previous results only while the role filter is the same: results of another reason
    // may hold partners of the wrong role and must not be selectable while the new search loads.
    placeholderData: (previous: PartnerSearchItem[] | undefined, previousQuery) =>
      previousQuery && sameRoleFilter(previousQuery.queryKey, queryKey) ? previous : undefined,
  });
}

type PartnerSearchKey = ReturnType<typeof stockVoucherKeys.partners>;

// The last key part is { type, keyword, reasonId }; only the keyword may differ.
function sameRoleFilter(previous: readonly unknown[], next: PartnerSearchKey): boolean {
  const before = previous[previous.length - 1] as PartnerSearchKey[3] | undefined;
  const after = next[3];
  return before?.type === after.type && before?.reasonId === after.reasonId;
}

// Every voucher change moves stock, costs and numbering: refresh the whole inventory root.
// A stock-in change also makes the server recompute Product.CostPrice (D35).
function useInvalidateInventory() {
  const qc = useQueryClient();
  return (type: StockDirection) => {
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
    if (type === 'In') void qc.invalidateQueries({ queryKey: productKeys.all });
  };
}

export function useCreateStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (data: UpsertStockVoucherRequest) => stockVouchersApi.create(data),
    onSuccess: (saved: StockVoucher) => invalidate(saved.type),
  });
}

export function useUpdateStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertStockVoucherRequest }) =>
      stockVouchersApi.update(id, data),
    onSuccess: (saved: StockVoucher) => invalidate(saved.type),
  });
}

export function useCancelStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: StockVoucherActionRequest }) =>
      stockVouchersApi.cancel(id, body),
    onSuccess: (saved: StockVoucher) => invalidate(saved.type),
  });
}

export function useRestoreStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: StockVoucherActionRequest }) =>
      stockVouchersApi.restore(id, body),
    onSuccess: (saved: StockVoucher) => invalidate(saved.type),
  });
}

export function useDeleteStockVoucher() {
  const qc = useQueryClient();
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; type: StockDirection; body: StockVoucherActionRequest }) =>
      stockVouchersApi.remove(id, body),
    onSuccess: (_data, { id, type }) => {
      // Drop the deleted voucher first so the invalidation does not refetch it into a 404.
      qc.removeQueries({ queryKey: stockVoucherKeys.detail(id) });
      invalidate(type);
    },
  });
}
