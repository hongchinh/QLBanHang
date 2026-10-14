import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { executiveApi } from './api';
import { executiveKeys } from './keys';
import type {
  BranchParam,
  ExecutiveParams,
  SalesTopBy,
  SalesTopSort,
  SeriesGranularity,
} from './types';

// Every executive query spreads these: the live signal refetches at once, and this interval is the fallback
// for a missed signal (only while the tab is visible). When the key changes (period, scope, granularity) the
// data of the previous key stays as placeholder (`isPlaceholderData`), so a tile keeps its content on screen
// instead of falling back to its skeleton.
export const executiveQueryOptions = {
  staleTime: 30_000,
  refetchInterval: 60_000,
  refetchIntervalInBackground: false,
  placeholderData: keepPreviousData,
} as const;

// `enabled: false` keeps the request of a domain the user may not see from being sent.
interface Options {
  enabled?: boolean;
}

export function useSalesSummary(params: ExecutiveParams, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.salesSummary(params),
    queryFn: () => executiveApi.salesSummary(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useSalesSeries(params: ExecutiveParams & { granularity: SeriesGranularity }, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.salesSeries(params),
    queryFn: () => executiveApi.salesSeries(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useSalesTop(
  params: ExecutiveParams & { by: SalesTopBy; sort?: SalesTopSort; limit?: number },
  options: Options = {},
) {
  return useQuery({
    queryKey: executiveKeys.salesTop(params),
    queryFn: () => executiveApi.salesTop(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useCashSummary(params: ExecutiveParams, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.cashSummary(params),
    queryFn: () => executiveApi.cashSummary(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useCashSeries(params: ExecutiveParams & { granularity: SeriesGranularity }, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.cashSeries(params),
    queryFn: () => executiveApi.cashSeries(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useCashBreakdown(params: ExecutiveParams, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.cashBreakdown(params),
    queryFn: () => executiveApi.cashBreakdown(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useDebtSummary(params: BranchParam, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.debtSummary(params),
    queryFn: () => executiveApi.debtSummary(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useDebtBreakdown(params: BranchParam, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.debtBreakdown(params),
    queryFn: () => executiveApi.debtBreakdown(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useInventorySummary(params: ExecutiveParams, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.inventorySummary(params),
    queryFn: () => executiveApi.inventorySummary(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useInventoryBreakdown(params: BranchParam, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.inventoryBreakdown(params),
    queryFn: () => executiveApi.inventoryBreakdown(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useBranchComparison(params: { from: string; to: string }, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.branches(params),
    queryFn: () => executiveApi.branches(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useAttention(params: BranchParam, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.attention(params),
    queryFn: () => executiveApi.attention(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}

export function useRecentDocuments(params: BranchParam & { limit?: number }, options: Options = {}) {
  return useQuery({
    queryKey: executiveKeys.recent(params),
    queryFn: () => executiveApi.recent(params),
    enabled: options.enabled ?? true,
    ...executiveQueryOptions,
  });
}
