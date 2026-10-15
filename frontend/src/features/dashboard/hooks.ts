import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from './api';
import { dashboardKeys } from './keys';
import type { DashboardParams, Granularity } from './types';

// The live signal refetches at once; this interval is the fallback for a missed signal or a lost connection
// (only while the tab is visible), as for the executive dashboard.
const liveQueryOptions = {
  staleTime: 30_000,
  refetchInterval: 60_000,
  refetchIntervalInBackground: false,
} as const;

export function useQuotationStats(params?: { from?: string; to?: string }) {
  return useQuery({
    queryKey: dashboardKeys.quotationStats(params),
    queryFn: () => dashboardApi.getQuotationStats(params),
  });
}

export function useDashboardSummary(params: { from?: string; to?: string; saleUserId?: string }) {
  return useQuery({
    queryKey: dashboardKeys.summary(params),
    queryFn: () => dashboardApi.getSummary(params),
    ...liveQueryOptions,
  });
}

export function useRevenueSeries(params: DashboardParams & { granularity: Granularity }) {
  return useQuery({
    queryKey: dashboardKeys.revenueSeries(params),
    queryFn: () => dashboardApi.getRevenueSeries(params),
    ...liveQueryOptions,
  });
}

export function useTopCustomers(params: DashboardParams & { limit?: number }) {
  return useQuery({
    queryKey: dashboardKeys.topCustomers(params),
    queryFn: () => dashboardApi.getTopCustomers(params),
    ...liveQueryOptions,
  });
}

export function useTopProducts(params: DashboardParams & { limit?: number }) {
  return useQuery({
    queryKey: dashboardKeys.topProducts(params),
    queryFn: () => dashboardApi.getTopProducts(params),
    ...liveQueryOptions,
  });
}

export function useRecentActivity(params: { limit?: number }) {
  return useQuery({
    queryKey: dashboardKeys.recentActivity(params),
    queryFn: () => dashboardApi.getRecentActivity(params),
    ...liveQueryOptions,
  });
}

export function useLeaderboard(params: { from: string; to: string; limit?: number }, enabled = true) {
  return useQuery({
    queryKey: dashboardKeys.leaderboard(params),
    queryFn: () => dashboardApi.getLeaderboard(params),
    ...liveQueryOptions,
    enabled,
  });
}
