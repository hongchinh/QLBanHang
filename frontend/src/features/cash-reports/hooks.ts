import { useQuery } from '@tanstack/react-query';
import { cashReportsApi } from './api';
import { cashReportKeys } from './keys';
import type { CashBookParams, DebtBalanceParams, DebtLedgerParams } from './types';

// `enabled: false` lets a page hold the request while its filters are invalid (e.g. from > to).

export function useDebtBalance(params: DebtBalanceParams, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: cashReportKeys.debtBalance(params),
    queryFn: () => cashReportsApi.debtBalance(params),
    enabled: options.enabled ?? true,
  });
}

export function useDebtLedger(params: DebtLedgerParams, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: cashReportKeys.debtLedger(params),
    queryFn: () => cashReportsApi.debtLedger(params),
    enabled: !!params.partnerId && (options.enabled ?? true),
  });
}

export function useCashBook(params: CashBookParams, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: cashReportKeys.cashBook(params),
    queryFn: () => cashReportsApi.cashBook(params),
    enabled: options.enabled ?? true,
  });
}
