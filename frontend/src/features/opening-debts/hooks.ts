import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { getApiError } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { openingDebtsApi } from './api';
import { openingDebtKeys } from './keys';
import type { OpeningDebtListParams, UpsertOpeningDebtRequest } from './types';

export function useOpeningDebts(params: OpeningDebtListParams) {
  return useQuery({
    queryKey: openingDebtKeys.list(params),
    queryFn: () => openingDebtsApi.list(params),
    placeholderData: keepPreviousData,
  });
}

// List filter: partners of every role (the form picker uses useDebtPartnerSearch of the chosen side).
export function useOpeningDebtPartnerSearch(keyword: string) {
  const trimmed = keyword.trim();
  return useQuery({
    queryKey: openingDebtKeys.partners(trimmed),
    queryFn: () => openingDebtsApi.searchPartners(trimmed),
    enabled: trimmed.length > 0,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });
}

// An opening debt moves balances and may drop settlements, which recomputes the Auto settlement of a stock
// voucher (P28): refresh both roots.
function useInvalidateOpeningDebts() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: cashKeys.all });
    void qc.invalidateQueries({ queryKey: inventoryKeys.all });
  };
}

// A stale version (D29): reload the list so the next edit or delete starts from the saved row.
function invalidateOnConcurrency(invalidate: () => void) {
  return (error: unknown) => {
    if (getApiError(error)?.code === 'CONCURRENCY') invalidate();
  };
}

export function useCreateOpeningDebt() {
  const invalidate = useInvalidateOpeningDebts();
  return useMutation({
    mutationFn: (data: UpsertOpeningDebtRequest) => openingDebtsApi.create(data),
    onSuccess: invalidate,
  });
}

export function useUpdateOpeningDebt() {
  const invalidate = useInvalidateOpeningDebts();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpsertOpeningDebtRequest }) => openingDebtsApi.update(id, data),
    onSuccess: invalidate,
    onError: invalidateOnConcurrency(invalidate),
  });
}

// Returns a handler that shows the OPENING_DATE_NOT_SET toast with a link to Settings → Chi nhánh;
// it returns false for any other error.
export function useOpeningDateNotSetHandler() {
  const navigate = useNavigate();
  return (error: unknown): boolean => {
    const apiError = getApiError(error);
    if (apiError?.code !== 'OPENING_DATE_NOT_SET') return false;
    toast({
      variant: 'destructive',
      title: 'Chưa đặt ngày đầu kỳ',
      description: apiError.message,
      action: { label: 'Mở Cài đặt → Chi nhánh', onClick: () => navigate('/settings/branches') },
    });
    return true;
  };
}

export function useDeleteOpeningDebt() {
  const invalidate = useInvalidateOpeningDebts();
  return useMutation({
    mutationFn: ({
      id,
      version,
      confirmRemoveSettlements,
      confirmedSettlements,
    }: {
      id: string;
      version: number;
      confirmRemoveSettlements: boolean;
      confirmedSettlements?: string;
    }) => openingDebtsApi.remove(id, version, confirmRemoveSettlements, confirmedSettlements),
    onSuccess: invalidate,
    onError: invalidateOnConcurrency(invalidate),
  });
}
