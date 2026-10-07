import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { stockVouchersApi } from './api';
import { inventoryKeys, stockVoucherKeys } from './keys';
import type {
  StockAtRequest,
  StockDirection,
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
export function useStockVoucherDefaults(
  type: StockDirection,
  voucherAt?: string,
  opts: { enabled?: boolean } = {},
) {
  return useQuery({
    queryKey: stockVoucherKeys.defaults(type, voucherAt),
    queryFn: () => stockVouchersApi.defaults(type, voucherAt),
    enabled: opts.enabled ?? true,
    placeholderData: keepPreviousData,
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
  return useQuery({
    queryKey: stockVoucherKeys.partners(type, trimmed, reasonId),
    queryFn: () => stockVouchersApi.partners(type, trimmed, reasonId),
    enabled: trimmed.length > 0,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });
}

// Every voucher change moves stock, costs and numbering: refresh the whole inventory root.
function useInvalidateInventory() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
  };
}

export function useCreateStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (data: UpsertStockVoucherRequest) => stockVouchersApi.create(data),
    onSuccess: invalidate,
  });
}

export function useUpdateStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertStockVoucherRequest }) =>
      stockVouchersApi.update(id, data),
    onSuccess: invalidate,
  });
}

export function useCancelStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: StockVoucherActionRequest }) =>
      stockVouchersApi.cancel(id, body),
    onSuccess: invalidate,
  });
}

export function useRestoreStockVoucher() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: StockVoucherActionRequest }) =>
      stockVouchersApi.restore(id, body),
    onSuccess: invalidate,
  });
}

export function useDeleteStockVoucher() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: StockVoucherActionRequest }) =>
      stockVouchersApi.remove(id, body),
    onSuccess: (_data, { id }) => {
      // Drop the deleted voucher first so the invalidation does not refetch it into a 404.
      qc.removeQueries({ queryKey: stockVoucherKeys.detail(id) });
      void qc.invalidateQueries({ queryKey: inventoryKeys.all });
    },
  });
}
